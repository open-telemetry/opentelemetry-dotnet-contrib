// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AWS;
using OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSMeterProviderTests
{
    [Fact]
    public void GetMeter_AfterDispose_ReturnsUsableHandleForTheSameScope()
    {
        const string Scope = "OpenTelemetry.Instrumentation.AWS.Tests.ReusedMeter";
        const string CounterName = "reused_counter";
        const long ActiveHandleCount = 5;

        var exportedItems = new List<Metric>();
        Amazon.Runtime.Telemetry.Metrics.Meter disposedMeter;
        Amazon.Runtime.Telemetry.Metrics.Meter reusedMeter;

        using (var metricProvider = Sdk.CreateMeterProviderBuilder()
                   .AddMeter(Scope)
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            var awsMeterProvider = new AWSMeterProvider(AWSSemanticConventions.DefaultSemanticConventionVersion);
            disposedMeter = awsMeterProvider.GetMeter(Scope);
            var disposedCounter = disposedMeter.CreateUpDownCounter<long>(CounterName);

            disposedMeter.Dispose();
            disposedCounter.Add(100);

            reusedMeter = awsMeterProvider.GetMeter(Scope);
            var reusedCounter = reusedMeter.CreateUpDownCounter<long>(CounterName);
            reusedCounter.Add(ActiveHandleCount);

            metricProvider.ForceFlush();
        }

        var counterMetric = Assert.Single(exportedItems, i => i.MeterName == Scope && i.Name == CounterName);
        var metricPoints = new List<MetricPoint>();
        foreach (var metricPoint in counterMetric.GetMetricPoints())
        {
            metricPoints.Add(metricPoint);
        }

        var singleMetricPoint = Assert.Single(metricPoints);

        Assert.NotSame(disposedMeter, reusedMeter);
        Assert.Equal(ActiveHandleCount, singleMetricPoint.GetSumLong());
    }
}
