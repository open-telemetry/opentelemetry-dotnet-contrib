// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Web;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class BasicTests
{
    [Fact]
    public void AddAspNetInstrumentation_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddAspNetInstrumentation());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void SpansAndMetricsGeneratedOnlyWhenEnabled(bool tracesEnabled, bool metricsEnabled)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();
        if (tracesEnabled)
        {
            tracerProviderBuilder
                .AddAspNetInstrumentation()
                .AddInMemoryExporter(activities);
        }

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder();
        if (metricsEnabled)
        {
            meterProviderBuilder = meterProviderBuilder
                .AddAspNetInstrumentation()
                .AddInMemoryExporter(metrics);
        }

        using var tracerProvider = tracerProviderBuilder.Build();
        using var meterProvider = meterProviderBuilder.Build();

        HttpContext.Current = RouteTestHelper.BuildHttpContext("http://localhost", 0, null, "GET");
        HttpContext.Current.Response.StatusCode = 200;
        var requestActivity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
        Thread.Sleep(1); // Make sure duration is always greater than 0 to avoid flakiness.
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, requestActivity, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();

        Activity? activity = null;

        if (tracesEnabled)
        {
            activity = Assert.Single(activities);
        }
        else
        {
            Assert.Empty(activities);
        }

        if (metricsEnabled)
        {
            var metric = Assert.Single(metrics);
            var metricPoints = new List<MetricPoint>();
            foreach (var p in metric.GetMetricPoints())
            {
                metricPoints.Add(p);
            }

            var metricPoint = Assert.Single(metricPoints);
            var measurementCount = metricPoint.GetHistogramCount();
            Assert.Equal(1, measurementCount);

            var sum = metricPoint.GetHistogramSum();

            Assert.True(sum > 0, "Sum should be greater than 0");
        }
        else
        {
            Assert.Empty(metrics);
        }
    }
}
