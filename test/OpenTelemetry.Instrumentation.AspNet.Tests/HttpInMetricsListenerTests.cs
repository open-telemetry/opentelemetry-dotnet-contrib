// <copyright file="HttpInMetricsListenerTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Web;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class HttpInMetricsListenerTests
{
    [Theory]
    [InlineData("http://localhost/", 0, null, null, "http", "localhost", null, 80)]
    [InlineData("https://localhost/", 0, null, null, "https", "localhost", null, 443)]
    [InlineData("http://localhost/api/value", 0, null, null, "http", "localhost", null, 80)]
    [InlineData("http://localhost/api/value", 1, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80)]
    [InlineData("http://localhost/api/value", 2, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80)]
    [InlineData("http://localhost/api/value", 3, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80)]
    [InlineData("http://localhost/api/value", 4, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 80)]
    [InlineData("http://localhost:8080/api/value", 0, null, null, "http", "localhost", null, 8080)]
    [InlineData("http://localhost:8080/api/value", 1, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 8080)]
    [InlineData("http://localhost:8080/api/value", 3, "{controller}/{action}", "enrich", "http", "localhost", "{controller}/{action}", 8080)]
    [InlineData("http://localhost:8080/api/value", 3, "{controller}/{action}", "throw", "http", "localhost", "{controller}/{action}", 8080)]
    [InlineData("http://localhost:8080/api/value", 3, "{controller}/{action}", null, "http", "localhost", "{controller}/{action}", 8080)]
    public void AspNetMetricTagsAreCollectedSuccessfully(
        string url,
        int routeType,
        string routeTemplate,
        string enrichMode,
        string expectedScheme,
        string expectedHost,
        string expectedRoute,
        int? expectedPort)
    {
        double duration = 0;
        HttpContext.Current = RouteTestHelper.BuildHttpContext(url, routeType, routeTemplate);

        // This is to enable activity creation
        // as it is created using ActivitySource inside TelemetryHttpModule
        // TODO: This should not be needed once the dependency on activity is removed from metrics
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetInstrumentation(opts => opts.Enrich
                = (activity, eventName, rawObject) =>
                {
                    if (eventName.Equals("OnStopActivity"))
                    {
                        duration = activity.Duration.TotalMilliseconds;
                    }
                })
            .Build();

        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetInstrumentation(options =>
            {
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
        Assert.Equal("http.server.duration", exportedItems[0].Name);
        Assert.Equal(1L, count);
        Assert.Equal(duration, sum);
        Assert.True(duration > 0, "Metric duration should be set.");

        var expectedTagCount = 3;
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

        ExpectTag("GET", SemanticConventions.AttributeHttpMethod);
        ExpectTag(200, SemanticConventions.AttributeHttpStatusCode);
        ExpectTag(expectedScheme, SemanticConventions.AttributeHttpScheme);

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
