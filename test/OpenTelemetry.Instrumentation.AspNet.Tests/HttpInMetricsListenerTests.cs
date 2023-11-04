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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class HttpInMetricsListenerTests
{
    [Fact]
    public void HttpDurationMetricIsEmitted()
    {
        string url = "http://localhost/api/value";
        double duration = 0;
        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, url, string.Empty),
            new HttpResponse(new StringWriter()));

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
            .AddAspNetInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        meterProvider.ForceFlush();

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

        Assert.Equal(3, metricPoints[0].Tags.Count);
        string? httpMethod = null;
        int httpStatusCode = 0;
        string? httpScheme = null;

        foreach (var tag in metricPoints[0].Tags)
        {
            if (tag.Key == SemanticConventions.AttributeHttpMethod)
            {
                httpMethod = (string)tag.Value;
                continue;
            }

            if (tag.Key == SemanticConventions.AttributeHttpStatusCode)
            {
                httpStatusCode = (int)tag.Value;
                continue;
            }

            if (tag.Key == SemanticConventions.AttributeHttpScheme)
            {
                httpScheme = (string)tag.Value;
                continue;
            }
        }

        Assert.Equal("GET", httpMethod);
        Assert.Equal(200, httpStatusCode);
        Assert.Equal("http", httpScheme);
    }

    [Fact]
    public void HttpDurationMetricIsEmittedWithEnrichments()
    {
        // Custom tag that will be added to the http.server.duration metric's TagList
        KeyValuePair<string, object?> testTag = new("testTagKey", "testTagValue");

        string url = "http://localhost/api/value";
        double duration = 0;
        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, url, string.Empty),
            new HttpResponse(new StringWriter()));

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
                options.Enrich = (string metricName, HttpContext httpContext, ref TagList tags) =>
                {
                    // If we're emitting an http.server.duration metric...
                    if (metricName == "http.server.duration")
                    {
                        // Enrich it with the given custom tag.
                        tags.Add(testTag);
                    }
                };
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        meterProvider.ForceFlush();

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

        Assert.Equal(4, metricPoints[0].Tags.Count);
        string? httpMethod = null;
        int httpStatusCode = 0;
        string? httpScheme = null;
        string? testTagVal = null;

        foreach (var tag in metricPoints[0].Tags)
        {
            if (tag.Key == SemanticConventions.AttributeHttpMethod)
            {
                httpMethod = (string)tag.Value;
                continue;
            }

            if (tag.Key == SemanticConventions.AttributeHttpStatusCode)
            {
                httpStatusCode = (int)tag.Value;
                continue;
            }

            if (tag.Key == SemanticConventions.AttributeHttpScheme)
            {
                httpScheme = (string)tag.Value;
                continue;
            }

            if (tag.Key == testTag.Key)
            {
                testTagVal = (string)tag.Value;
                continue;
            }
        }

        Assert.Equal("GET", httpMethod);
        Assert.Equal(200, httpStatusCode);
        Assert.Equal("http", httpScheme);
        Assert.Equal(testTag.Value, testTagVal);
    }

    [Fact]
    public void HttpDurationMetricIsFiltered()
    {
        // Make two "requests" and filter the http.server.duration to only collect
        // metrics for one of the given URLs.
        string url1 = "http://localhost/api/filterout";
        string url2 = "http://localhost/api/collect";
        double duration = 0;
        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, url1, string.Empty),
            new HttpResponse(new StringWriter()));

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
                options.Filter = (string metricName, HttpContext httpContext) =>
                {
                    // If the request hits a given URL, we shouldn't emit a metric.
                    return httpContext.Request.Url.AbsoluteUri != url1;
                };
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        var activity1 = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity1, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, url2, string.Empty),
            new HttpResponse(new StringWriter()));

        var activity2 = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity2, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        meterProvider.ForceFlush();

        // We should've only collected one metric
        Assert.Single(exportedItems);
    }

    [Fact]
    public void HttpDurationMetricIsEmittedWithEnrichmentsAndFiltered()
    {
        // Custom tag that will be added to the http.server.duration metric's TagList
        KeyValuePair<string, object?> testTag = new("testTagKey", "testTagValue");

        // Make two "requests" and filter the http.server.duration to only collect
        // metrics for one of the given URLs.
        string url1 = "http://localhost/api/filterout";
        string url2 = "http://localhost/api/collect";
        double duration = 0;
        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, url1, string.Empty),
            new HttpResponse(new StringWriter()));

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
                options.Enrich = (string metricName, HttpContext httpContext, ref TagList tags) =>
                {
                    // If we're emitting an http.server.duration metric...
                    if (metricName == "http.server.duration")
                    {
                        // Enrich it with the given custom tag.
                        tags.Add(testTag);
                    }
                };
                options.Filter = (string metricName, HttpContext httpContext) =>
                {
                    // If the request hits a given URL, we shouldn't emit a metric.
                    return httpContext.Request.Url.AbsoluteUri != url1;
                };
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        var activity1 = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity1, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, url2, string.Empty),
            new HttpResponse(new StringWriter()));

        var activity2 = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity2, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        meterProvider.ForceFlush();

        // We should've only collected one metric
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

        Assert.Equal(4, metricPoints[0].Tags.Count);
        string? httpMethod = null;
        int httpStatusCode = 0;
        string? httpScheme = null;
        string? testTagVal = null;

        foreach (var tag in metricPoints[0].Tags)
        {
            if (tag.Key == SemanticConventions.AttributeHttpMethod)
            {
                httpMethod = (string)tag.Value;
                continue;
            }

            if (tag.Key == SemanticConventions.AttributeHttpStatusCode)
            {
                httpStatusCode = (int)tag.Value;
                continue;
            }

            if (tag.Key == SemanticConventions.AttributeHttpScheme)
            {
                httpScheme = (string)tag.Value;
                continue;
            }

            if (tag.Key == testTag.Key)
            {
                testTagVal = (string)tag.Value;
                continue;
            }
        }

        Assert.Equal("GET", httpMethod);
        Assert.Equal(200, httpStatusCode);
        Assert.Equal("http", httpScheme);
        Assert.Equal(testTag.Value, testTagVal);
    }
}
