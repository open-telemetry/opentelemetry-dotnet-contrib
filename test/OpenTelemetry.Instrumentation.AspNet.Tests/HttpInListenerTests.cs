// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Routing;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNet.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class HttpInListenerTests
{
    [Theory]
    [InlineData("http://localhost/", "http", "/", null, "localhost", 80, "GET", "GET", null, 0, null)]
    [InlineData("http://localhost/?foo=bar&baz=test", "http", "/",  "foo=bar&baz=test", "localhost", 80, "POST", "POST", null, 0, null, true)]
    [InlineData("https://localhost/", "https", "/",  null, "localhost", 443, "NonStandard", "_OTHER", "NonStandard", 0, null)]
    [InlineData("https://user:pass@localhost/", "https", "/", null,  "localhost", 443, "GET", "GET", null, 0, null)] // Test URL sanitization
    [InlineData("http://localhost:443/", "http", "/", null, "localhost", 443, "GET", "GET", null, 0, null)] // Test http over 443
    [InlineData("https://localhost:80/", "https", "/", null, "localhost", 80, "GET", "GET", null, 0, null)] // Test https over 80
    [InlineData("https://localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", "https", "/Home/Index.htm", "q1=v1&q2=v2", "localhost", 80, "GET", "GET", null, 0, null)] // Test complex URL
    [InlineData("https://user:password@localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", "https", "/Home/Index.htm", "q1=v1&q2=v2",  "localhost", 80, "GET", "GET", null, 0, null)] // Test complex URL sanitization
    [InlineData("http://localhost:80/Index", "http", "/Index", null, "localhost", 80, "GET", "GET", null, 1, "{controller}/{action}/{id}")]
    [InlineData("https://localhost:443/about_attr_route/10", "https", "/about_attr_route/10", null,  "localhost", 443, "GET", "GET", null, 2, "about_attr_route/{customerId}")]
    [InlineData("http://localhost:1880/api/weatherforecast", "http", "/api/weatherforecast", null, "localhost", 1880, "GET", "GET", null, 3, "api/{controller}/{id}")]
    [InlineData("https://localhost:1843/subroute/10", "https", "/subroute/10", null, "localhost", 1843, "GET", "GET", null, 4, "subroute/{customerId}")]
    [InlineData("http://localhost/api/value", "http", "/api/value", null, "localhost", 80, "GET", "GET", null, 0, null, false, "/api/value")] // Request will be filtered
    [InlineData("http://localhost/api/value", "http", "/api/value", null, "localhost", 80, "GET", "GET", null, 0, null, false, "{ThrowException}")] // Filter user code will throw an exception
    [InlineData("http://localhost/", "http", "/", null, "localhost", 80, "GET", "GET", null, 0, null, false, null, true, "System.InvalidOperationException")] // Test RecordException option
    public void AspNetRequestsAreCollectedSuccessfully(
        string url,
        string expectedUrlScheme,
        string expectedUrlPath,
        string expectedUrlQuery,
        string expectedHost,
        int expectedPort,
        string requestMethod,
        string expectedRequestMethod,
        string? expectedOriginalRequestMethod,
        int routeType,
        string routeTemplate,
        bool setStatusToErrorInEnrich = false,
        string? filter = null,
        bool recordException = false,
        string? expectedErrorType = null)
    {
        HttpContext.Current = RouteTestHelper.BuildHttpContext(url, routeType, routeTemplate, requestMethod);

        typeof(HttpRequest).GetField("_wr", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(HttpContext.Current.Request, new TestHttpWorkerRequest());

        List<Activity> exportedItems = new List<Activity>(16);

        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
        using (Sdk.CreateTracerProviderBuilder()
                   .AddAspNetInstrumentation((options) =>
                   {
                       options.Filter = httpContext =>
                       {
                           Assert.True(Activity.Current!.IsAllDataRequested);
                           if (string.IsNullOrEmpty(filter))
                           {
                               return true;
                           }

                           if (filter == "{ThrowException}")
                           {
                               throw new InvalidOperationException();
                           }

                           return httpContext.Request.Path != filter;
                       };

                       options.Enrich = GetEnrichmentAction(setStatusToErrorInEnrich ? ActivityStatusCode.Error : default);

                       options.RecordException = recordException;
                   })
                   .AddInMemoryExporter(exportedItems)
                   .Build())
        {
            using var inMemoryEventListener = new InMemoryEventListener(AspNetInstrumentationEventSource.Log);

            var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);

            if (filter == "{ThrowException}")
            {
                Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 2));
            }

            Assert.Equal(TelemetryHttpModule.AspNetActivityName, Activity.Current!.OperationName);

            if (recordException)
            {
                ActivityHelper.WriteActivityException(activity, HttpContext.Current, new InvalidOperationException(), TelemetryHttpModule.Options.OnExceptionCallback);
            }

            ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);
        }

        if (HttpContext.Current.Request.Path == filter || filter == "{ThrowException}")
        {
            Assert.Empty(exportedItems);
            return;
        }

        Assert.Single(exportedItems);

        Activity span = exportedItems[0];

        Assert.Equal(TelemetryHttpModule.AspNetActivityName, span.OperationName);
        Assert.NotEqual(TimeSpan.Zero, span.Duration);

        Assert.Equal(routeTemplate ?? HttpContext.Current.Request.Path, span.DisplayName);
        Assert.Equal(ActivityKind.Server, span.Kind);
        Assert.True(span.Duration != TimeSpan.Zero);

        Assert.Equal(200, span.GetTagValue("http.response.status_code"));

        Assert.Equal(expectedHost, span.GetTagValue("server.address"));
        Assert.Equal(expectedPort, span.GetTagValue("server.port"));

        Assert.Equal(expectedRequestMethod, span.GetTagValue("http.request.method"));
        Assert.Equal(expectedOriginalRequestMethod, span.GetTagValue("http.request.method_original"));
        Assert.Equal("FakeHTTP/123", span.GetTagValue("network.protocol.version"));

        Assert.Equal(expectedUrlPath, span.GetTagValue("url.path"));
        Assert.Equal(expectedUrlQuery, span.GetTagValue("url.query"));
        Assert.Equal(expectedUrlScheme, span.GetTagValue("url.scheme"));
        Assert.Equal("Custom User Agent v1.2.3", span.GetTagValue("user_agent.original"));
        Assert.Equal(expectedErrorType, span.GetTagValue("error.type"));

        if (recordException)
        {
            var status = span.Status;
            Assert.Equal(ActivityStatusCode.Error, span.Status);
            Assert.Equal("Operation is not valid due to the current state of the object.", span.StatusDescription);
        }
        else if (setStatusToErrorInEnrich)
        {
            // This validates that users can override the
            // status in Enrich.
            Assert.Equal(ActivityStatusCode.Error, span.Status);

            // Instrumentation is not expected to set status description
            // as the reason can be inferred from SemanticConventions.AttributeHttpStatusCode
            Assert.True(string.IsNullOrEmpty(span.StatusDescription));
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Unset, span.Status);

            // Instrumentation is not expected to set status description
            // as the reason can be inferred from SemanticConventions.AttributeHttpStatusCode
            Assert.True(string.IsNullOrEmpty(span.StatusDescription));
        }
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void ExtractContextIrrespectiveOfSamplingDecision(SamplingDecision samplingDecision)
    {
        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, "http://localhost/", string.Empty)
            {
                RequestContext = new RequestContext()
                {
                    RouteData = new RouteData(),
                },
            },
            new HttpResponse(new StringWriter()));

        bool isPropagatorCalled = false;
        var propagator = new TestTextMapPropagator
        {
            Extracted = () => isPropagatorCalled = true,
        };

        var activityProcessor = new TestActivityProcessor();
        Sdk.SetDefaultTextMapPropagator(propagator);
        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .SetSampler(new TestSampler(samplingDecision))
                   .AddAspNetInstrumentation()
                   .AddProcessor(activityProcessor).Build())
        {
            var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
            ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);
        }

        Assert.True(isPropagatorCalled);
    }

    [Fact]
    public void ExtractContextIrrespectiveOfTheFilterApplied()
    {
        HttpContext.Current = new HttpContext(
            new HttpRequest(string.Empty, "http://localhost/", string.Empty)
            {
                RequestContext = new RequestContext()
                {
                    RouteData = new RouteData(),
                },
            },
            new HttpResponse(new StringWriter()));

        bool isPropagatorCalled = false;
        var propagator = new TestTextMapPropagator
        {
            Extracted = () => isPropagatorCalled = true,
        };

        bool isFilterCalled = false;
        var activityProcessor = new TestActivityProcessor();
        Sdk.SetDefaultTextMapPropagator(propagator);
        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAspNetInstrumentation(options =>
                   {
                       options.Filter = context =>
                       {
                           isFilterCalled = true;
                           return false;
                       };
                   })
                   .AddProcessor(activityProcessor).Build())
        {
            var activity = ActivityHelper.StartAspNetActivity(Propagators.DefaultTextMapPropagator, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStartedCallback);
            ActivityHelper.StopAspNetActivity(Propagators.DefaultTextMapPropagator, activity, HttpContext.Current, TelemetryHttpModule.Options.OnRequestStoppedCallback);
        }

        Assert.True(isFilterCalled);
        Assert.True(isPropagatorCalled);
    }

    private static Action<Activity, string, object> GetEnrichmentAction(ActivityStatusCode statusToBeSet)
    {
        void EnrichAction(Activity activity, string method, object obj)
        {
            Assert.True(activity.IsAllDataRequested);
            switch (method)
            {
                case "OnStartActivity":
                    Assert.True(obj is HttpRequest);
                    break;

                case "OnStopActivity":
                    Assert.True(obj is HttpResponse);
                    if (statusToBeSet != default)
                    {
                        activity.SetStatus(statusToBeSet);
                    }

                    break;

                case "OnException":
                    Assert.True(obj is Exception);
                    break;
            }
        }

        return EnrichAction;
    }

    private class TestSampler(SamplingDecision samplingDecision) : Sampler
    {
        private readonly SamplingDecision samplingDecision = samplingDecision;

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            return new SamplingResult(this.samplingDecision);
        }
    }
}
