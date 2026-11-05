// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Web;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNet.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class HttpInMetricsListenerTests
{
    [Fact]
    public async Task AspNetMetricDurationUsesContextWhenExecutionContextDoesNotFlow()
    {
        HttpContext.Current = RouteTestHelper.BuildHttpContext("http://localhost/", 0, null, "GET");

        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        var context = new HttpContextWrapper(HttpContext.Current);
        var activity = ActivityHelper.StartAspNetActivity(
            Propagators.DefaultTextMapPropagator,
            context,
            TelemetryHttpModule.Options.OnRequestStartedCallback);
        Assert.Null(activity);

        Task stopTask;

        // IIS can lose context across managed/unmanaged boundaries,
        // resetting AsyncLocal.
        // Calling `SuppressFlow` simulates this to ensure we rely on
        // HttpContext.Items instead of context being maintained.
        using (ExecutionContext.SuppressFlow())
        {
            stopTask = Task.Run(
                () => ActivityHelper.StopAspNetActivity(
                    Propagators.DefaultTextMapPropagator,
                    activity,
                    context,
                    TelemetryHttpModule.Options.OnRequestStoppedCallback),
                TestContext.Current.CancellationToken);
        }

        await stopTask;
        meterProvider.ForceFlush();

        var metric = Assert.Single(exportedItems);
        var metricPoints = new List<MetricPoint>();
        foreach (var point in metric.GetMetricPoints())
        {
            metricPoints.Add(point);
        }

        var metricPoint = Assert.Single(metricPoints);

        Assert.Equal(1L, metricPoint.GetHistogramCount());
        Assert.InRange(metricPoint.GetHistogramSum(), 0, 60);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AspNetExceptionMetricIsRecordedOnceWithFinalResponseStatus(bool tracingEnabled)
    {
        HttpContext.Current = RouteTestHelper.BuildHttpContext("http://localhost/", 0, null, "GET");

        var exportedItems = new List<Metric>();
        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();
        if (tracingEnabled)
        {
            tracerProviderBuilder.AddAspNetInstrumentation();
        }

        using var tracerProvider = tracerProviderBuilder.Build();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        var context = new HttpContextWrapper(HttpContext.Current);
        var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, context, TelemetryHttpModule.Options.OnRequestStartedCallback);
        Thread.Sleep(1); // Make sure duration is always greater than 0 to avoid flakiness.
        ActivityHelper.WriteActivityException(activity, context, new InvalidOperationException(), TelemetryHttpModule.Options.OnExceptionCallback);
        HttpContext.Current.Response.StatusCode = 500;
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, context, TelemetryHttpModule.Options.OnRequestStoppedCallback);

        meterProvider.ForceFlush();

        var metric = Assert.Single(exportedItems);
        var metricPoints = new List<MetricPoint>();
        foreach (var point in metric.GetMetricPoints())
        {
            metricPoints.Add(point);
        }

        var metricPoint = Assert.Single(metricPoints);

        Assert.Equal(1L, metricPoint.GetHistogramCount());
        Assert.True(metricPoint.GetHistogramSum() > 0, "Metric sum (duration) should be greater than 0.");

        var tags = new Dictionary<string, object?>(metricPoint.Tags.Count);
        foreach (var tag in metricPoint.Tags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        Assert.Equal(500, tags["http.response.status_code"]);
        Assert.Equal("System.InvalidOperationException", tags["error.type"]);
    }

    [Theory]
    [InlineData("http://localhost/", 0, null, null, "http", "localhost", null, 80, 200)]
    [InlineData("http://localhost/", 0, null, null, "http", null, null, null, 200, false)]
    [InlineData("https://localhost/", 0, null, null, "https", "localhost", null, 443, 200)]
    [InlineData("https://localhost/", 0, null, null, "https", null, null, null, 200, false)]
    [InlineData("http://localhost/api/Values/5", 0, null, null, "http", "localhost", null, 80, 200)]
    [InlineData("http://localhost/api/Values/5", 1, "api/{controller}/{id}", null, "http", "localhost", "api/Values/{id}", 80, 200)]
    [InlineData("http://localhost/api/Values/5", 2, "api/{controller}/{id}", null, "http", "localhost", "api/Values/{id}", 80, 201)]
    [InlineData("http://localhost/api/Values/5", 3, "api/{controller}/{id}", null, "http", "localhost", "api/Values/{id}", 80, 200)]
    [InlineData("http://localhost/api/Values/5", 4, "api/Values/{id}", null, "http", "localhost", "api/Values/{id}", 80, 200)]
    [InlineData("http://localhost/api/Unserved/5", 1, "api/{controller}/{id}", null, "http", "localhost", "api/{controller}/{id}", 80, 500)] // The route values are not trusted until a request is served successfully.
    [InlineData("http://localhost/api/Values/Details", 3, "api/{controller}/{actionName}", null, "http", "localhost", "api/Values/{actionName}", 80, 200)]
    [InlineData("http://localhost:8080/api/Values/5", 0, null, null, "http", "localhost", null, 8080, 200)]
    [InlineData("http://localhost:8080/api/Values/5", 1, "api/{controller}/{id}", null, "http", "localhost", "api/Values/{id}", 8080, 200)]
    [InlineData("http://localhost:8080/api/Values/5", 3, "api/{controller}/{id}", "enrich", "http", "localhost", "api/Values/{id}", 8080, 200)]
    [InlineData("http://localhost:8080/api/Values/5", 3, "api/{controller}/{id}", "throw", "http", "localhost", "api/Values/{id}", 8080, 200)]
    [InlineData("http://localhost:8080/api/Values/5", 3, "api/{controller}/{id}", null, "http", "localhost", "api/Values/{id}", 8080, 200)]
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
        HttpContext.Current = RouteTestHelper.BuildHttpContext(url, routeType, routeTemplate, "GET");
        HttpContext.Current.Response.StatusCode = expectedStatus;

        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetInstrumentation(options =>
            {
                options.EnableServerAttributesForRequestDuration = enableServerAttributesForRequestDuration;

                options.EnrichWithHttpContext += (context, ref tags) =>
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

        var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, new HttpContextWrapper(HttpContext.Current), TelemetryHttpModule.Options.OnRequestStartedCallback);
        Thread.Sleep(1); // Make sure duration is always greater than 0 to avoid flakiness.
        ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, new HttpContextWrapper(HttpContext.Current), TelemetryHttpModule.Options.OnRequestStoppedCallback);

        meterProvider.ForceFlush();

        var item = Assert.Single(exportedItems);

        var metricPoints = new List<MetricPoint>();
        foreach (var p in item.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        var metricPoint = Assert.Single(metricPoints);

        var count = metricPoint.GetHistogramCount();
        var sum = metricPoint.GetHistogramSum();

        Assert.Equal(MetricType.Histogram, exportedItems[0].MetricType);
        Assert.Equal("http.server.request.duration", exportedItems[0].Name);
        Assert.Equal("s", exportedItems[0].Unit);
        Assert.Equal(1L, count);
        Assert.True(sum > 0, "Metric sum (duration) should be greater than 0.");

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
        var tags = new Dictionary<string, object?>(metricPoint.Tags.Count);
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
            expected: [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, double.PositiveInfinity],
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

    [Fact]
    public void AspNetMetricRouteDoesNotContainControllerAndActionThatDoNotExist()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // The instrumentation, and so the set of names it has seen, is shared by all tests, so the names here are unique to this test.
        // System.Web routing binds the controller and action from the URL before it is known whether they exist.
        for (var i = 0; i < 100; i++)
        {
            Send($"http://localhost/x{i}/y{i}/1", 404);
        }

        // An error before the controller is known to exist does not make its name trusted.
        Send("http://localhost/Widgets/Show/1", 500);

        // The names of controllers and actions that were served are used, whatever the outcome or casing.
        Send("http://localhost/Gadgets/List/1", 200);
        Send("http://localhost/Gadgets/List/2", 500);
        Send("http://localhost/gAdGeTs/lIsT/3", 200);
        Send("http://localhost/Gadgets/Missing/4", 404);

        meterProvider.ForceFlush();

        var metric = Assert.Single(exportedItems);
        var routes = new Dictionary<string, long>();
        foreach (var point in metric.GetMetricPoints())
        {
            string? route = null;
            foreach (var tag in point.Tags)
            {
                if (tag.Key == "http.route")
                {
                    route = (string?)tag.Value;
                }
            }

            Assert.NotNull(route);
            routes[route] = routes.TryGetValue(route, out var count) ? count + point.GetHistogramCount() : point.GetHistogramCount();
        }

        Assert.Equal(2, routes.Count);
        Assert.Equal(100 + 1 + 1, routes["{controller}/{action}/{id}"]);
        Assert.Equal(3, routes["Gadgets/List/{id}"]);

        static void Send(string url, int statusCode)
        {
            HttpContext.Current = RouteTestHelper.BuildHttpContext(url, 1, "{controller}/{action}/{id}", "GET");
            HttpContext.Current.Response.StatusCode = statusCode;

            var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, new HttpContextWrapper(HttpContext.Current), TelemetryHttpModule.Options.OnRequestStartedCallback);
            ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, new HttpContextWrapper(HttpContext.Current), TelemetryHttpModule.Options.OnRequestStoppedCallback);
        }
    }

    [Fact]
    public void AspNetMetricRouteDoesNotTrustControllerAndActionOfRequestsThatHadAnExceptionWhoseErrorWasCleared()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // The instrumentation sees the exception when it is raised. The application then handles it, for example in
        // Application_Error by clearing the error and redirecting to an error page, so by the end of the request
        // HttpContext.Error is null and the status code is not an error. This is more requests than names are remembered for.
        for (var i = 0; i < HttpRequestRouteHelper.MaxResolvedTemplates + 100; i++)
        {
            Send($"http://localhost/missing{i}/page{i}/1", 302, new HttpException(404, "Not found"));
        }

        // Names that exist are still used afterwards.
        Send("http://localhost/Basket/View/1", 200, exception: null);
        Send("http://localhost/Basket/View/2", 200, exception: null);

        meterProvider.ForceFlush();

        var metric = Assert.Single(exportedItems);
        var routes = new Dictionary<string, long>();
        foreach (var point in metric.GetMetricPoints())
        {
            string? route = null;
            foreach (var tag in point.Tags)
            {
                if (tag.Key == "http.route")
                {
                    route = (string?)tag.Value;
                }
            }

            Assert.NotNull(route);
            routes[route] = routes.TryGetValue(route, out var count) ? count + point.GetHistogramCount() : point.GetHistogramCount();
        }

        Assert.Equal(2, routes.Count);
        Assert.Equal(HttpRequestRouteHelper.MaxResolvedTemplates + 100, routes["{controller}/{action}/{id}"]);
        Assert.Equal(2, routes["Basket/View/{id}"]);

        static void Send(string url, int statusCode, Exception? exception)
        {
            HttpContext.Current = RouteTestHelper.BuildHttpContext(url, 1, "{controller}/{action}/{id}", "GET");
            var context = new HttpContextWrapper(HttpContext.Current);

            var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, context, TelemetryHttpModule.Options.OnRequestStartedCallback);
            if (exception is not null)
            {
                ActivityHelper.WriteActivityException(activity, context, exception, TelemetryHttpModule.Options.OnExceptionCallback);
            }

            HttpContext.Current.Response.StatusCode = statusCode;
            ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, context, TelemetryHttpModule.Options.OnRequestStoppedCallback);
        }
    }
}
