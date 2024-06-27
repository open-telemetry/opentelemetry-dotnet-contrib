// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Web;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class HttpInMetricsListenerTests
{
    [Theory]
    [InlineData("http://localhost/", 0, null, null, "http", "localhost", null, 80, 200)]
    [InlineData("http://localhost/", 0, null, null, "http", null, null, null, 200, false)]
    [InlineData("https://localhost/", 0, null, null, "https", "localhost", null, 443, 200)]
    [InlineData("https://localhost/", 0, null, null, "https", null, null, null, 200, false)]
    [InlineData("http://localhost/api/value", 0, null, null, "http", "localhost", null, 80, 200)]
    [InlineData("http://localhost/api/value", 1, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80, 200)]
    [InlineData("http://localhost/api/value", 2, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80, 201)]
    [InlineData("http://localhost/api/value", 3, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80, 200)]
    [InlineData("http://localhost/api/value", 4, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80, 200)]
    [InlineData("http://localhost/api/value", 1, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80, 500)]
    [InlineData("http://localhost:8080/api/value", 0, null, null, "http", "localhost", null, 8080, 200)]
    [InlineData("http://localhost:8080/api/value", 1, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 8080, 200)]
    [InlineData("http://localhost:8080/api/value", 3, "{controller}/{action}", "enrich", "http", "localhost", "{controller}/{action}", 8080, 200)]
    [InlineData("http://localhost:8080/api/value", 3, "{controller}/{action}", "throw", "http", "localhost", "{controller}/{action}", 8080, 200)]
    [InlineData("http://localhost:8080/api/value", 3, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 8080, 200)]
    public void AspNetMetricTagsAreCollectedSuccessfully(
        string url,
        int routeType,
        string? routeTemplate,
        string? enrichMode,
        string expectedScheme,
        string? expectedHost,
        string? expectedRoute,
        int? expectedPort,
        int expectedStatus,
        bool enableServerAttributesForRequestDuration = true)
    {
        double duration = 0;
        HttpContext.Current = RouteTestHelper.BuildHttpContext(url, routeType, routeTemplate, "GET");
        HttpContext.Current.Response.StatusCode = expectedStatus;

        // This is to enable activity creation
        // as it is created using ActivitySource inside TelemetryHttpModule
        // TODO: This should not be needed once the dependency on activity is removed from metrics
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetInstrumentation(opts => opts.EnrichWithHttpResponse
                = (activity, response) =>
                {
                    duration = activity.Duration.TotalSeconds;
                })
            .Build();

        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetInstrumentation(options =>
            {
                options.EnableServerAttributesForRequestDuration = enableServerAttributesForRequestDuration;

                options.Enrich += (HttpContext context, ref TagList tags) =>
                {
                    if (enrichMode == "throw")
                    {
                        throw new Exception("Enrich exception");
                    }

                    if (enrichMode == "enrich")
                    {
                        tags.Add("enriched", "true");
                    }
                };
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
        Thread.Sleep(1); // Make sure duration is always greater than 0 to avoid flakiness.
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);

        var metricPoints = new List<MetricPoint>();
        foreach (var p in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        Assert.Single(metricPoints);

        var metricPoint = metricPoints[0];

        var count = metricPoint.GetHistogramCount();
        var sum = metricPoint.GetHistogramSum();

        Assert.Equal(MetricType.Histogram, exportedItems[0].MetricType);
        Assert.Equal("http.server.request.duration", exportedItems[0].Name);
        Assert.Equal("s", exportedItems[0].Unit);
        Assert.Equal(1L, count);
        Assert.Equal(duration, sum);
        Assert.True(duration > 0, "Metric duration should be set.");

        var expectedTagCount = 3;

        if (enableServerAttributesForRequestDuration)
        {
            expectedTagCount += 2;
        }

        if (!string.IsNullOrEmpty(expectedRoute))
        {
            expectedTagCount++;
        }

        if (enrichMode == "enrich")
        {
            expectedTagCount++;
        }

        Assert.Equal(expectedTagCount, metricPoints[0].Tags.Count);
        Dictionary<string, object?> tags = new(metricPoint.Tags.Count);
        foreach (var tag in metricPoint.Tags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        if (enrichMode == "enrich")
        {
            ExpectTag("true", "enriched");
        }

        // Do not use constants from SemanticConventions here in order to detect mistakes.
        // https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-metrics.md#http-server
        // Unable to check for "network.protocol.version" because we can't set server variables due to the accessibility
        // of the ServerVariables property.
        ExpectTag("GET", "http.request.method");
        ExpectTag(expectedStatus, "http.response.status_code");
        ExpectTag(expectedRoute, "http.route");
        ExpectTag(expectedHost, "server.address");
        ExpectTag(expectedPort, "server.port");
        ExpectTag(expectedScheme, "url.scheme");

        // Inspect histogram bucket boundaries.
        var histogramBuckets = metricPoint.GetHistogramBuckets();
        var histogramBounds = new List<double>();
        foreach (var t in histogramBuckets)
        {
            histogramBounds.Add(t.ExplicitBound);
        }

        Assert.Equal(
            expected: new List<double> { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, double.PositiveInfinity },
            actual: histogramBounds);

        void ExpectTag<T>(T? expected, string tagName)
        {
            if (expected is null)
            {
                Assert.DoesNotContain(tagName, tags.Keys);
                return;
            }

            if (tags.TryGetValue(tagName, out var value))
            {
                Assert.Equal(expected, (T?)value);
                return;
            }

            Assert.Fail($"Expected tag with key {tagName} not found.");
        }
    }
}
