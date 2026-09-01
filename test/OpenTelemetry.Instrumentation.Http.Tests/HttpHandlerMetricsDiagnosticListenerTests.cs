// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class HttpHandlerMetricsDiagnosticListenerTests
{
    [Fact]
    public void OnStopEventWrittenSetsServerPortTagForNonDefaultPort()
    {
        var metricItems = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(HttpHandlerMetricsDiagnosticListener.Meter.Name)
            .AddInMemoryExporter(metricItems)
            .Build();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://example.com:8080/"));
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Version = new Version(1, 1) };

        using var activity = new Activity("test").Start();

        var listener = new HttpHandlerMetricsDiagnosticListener("HttpHandlerDiagnosticListener");
        listener.OnEventWritten(HttpHandlerMetricsDiagnosticListener.OnStopEvent, new { Request = request, Response = response });

        meterProvider.Dispose();

        var metric = Assert.Single(metricItems, m => m.Name == "http.client.request.duration");

        var metricPoints = new List<MetricPoint>();
        foreach (var point in metric.GetMetricPoints())
        {
            metricPoints.Add(point);
        }

        var metricPoint = Assert.Single(metricPoints);

        var attributes = new Dictionary<string, object?>();
        foreach (var tag in metricPoint.Tags)
        {
            attributes[tag.Key] = tag.Value;
        }

        Assert.Equal(8080, attributes[SemanticConventions.AttributeServerPort]);
    }
}
