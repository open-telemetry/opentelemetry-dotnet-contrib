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
    [InlineData("http://localhost/", "http://localhost/", 0, null)]
    [InlineData("http://localhost/", "http://localhost/", 0, null, true)]
    [InlineData("https://localhost/", "https://localhost/", 0, null)]
    [InlineData("https://localhost/", "https://user:pass@localhost/", 0, null)] // Test URL sanitization
    [InlineData("http://localhost:443/", "http://localhost:443/", 0, null)] // Test http over 443
    [InlineData("https://localhost:80/", "https://localhost:80/", 0, null)] // Test https over 80
    [InlineData("https://localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", "https://localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", 0, null)] // Test complex URL
    [InlineData("https://localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", "https://user:password@localhost:80/Home/Index.htm?q1=v1&q2=v2#FragmentName", 0, null)] // Test complex URL sanitization
    [InlineData("http://localhost:80/Index", "http://localhost:80/Index", 1, "{controller}/{action}/{id}")]
    [InlineData("https://localhost:443/about_attr_route/10", "https://localhost:443/about_attr_route/10", 2, "about_attr_route/{customerId}")]
    [InlineData("http://localhost:1880/api/weatherforecast", "http://localhost:1880/api/weatherforecast", 3, "api/{controller}/{id}")]
    [InlineData("https://localhost:1843/subroute/10", "https://localhost:1843/subroute/10", 4, "subroute/{customerId}")]
    [InlineData("http://localhost/api/value", "http://localhost/api/value", 0, null, false, "/api/value")] // Request will be filtered
    [InlineData("http://localhost/api/value", "http://localhost/api/value", 0, null, false, "{ThrowException}")] // Filter user code will throw an exception
    [InlineData("http://localhost/", "http://localhost/", 0, null, false, null, true)] // Test RecordException option
    public void AspNetRequestsAreCollectedSuccessfully(
        string expectedUrl,
        string url,
        int routeType,
        string routeTemplate,
        bool setStatusToErrorInEnrich = false,
        string? filter = null,
        bool recordException = false)
    {
        HttpContext.Current = RouteTestHelper.BuildHttpContext(url, routeType, routeTemplate);

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

        Assert.Equal(200, span.GetTagValue(SemanticConventions.AttributeHttpStatusCode));

        var expectedUri = new Uri(expectedUrl);
        var actualUrl = span.GetTagValue(SemanticConventions.AttributeHttpUrl);

        Assert.Equal(expectedUri.ToString(), actualUrl);

        // Url strips 80 or 443 if the scheme matches.
        if ((expectedUri.Port == 80 && expectedUri.Scheme == "http") || (expectedUri.Port == 443 && expectedUri.Scheme == "https"))
        {
            Assert.DoesNotContain($":{expectedUri.Port}", actualUrl as string);
        }
        else
        {
            Assert.Contains($":{expectedUri.Port}", actualUrl as string);
        }

        // Host includes port if it isn't 80 or 443.
        if (expectedUri.Port is 80 or 443)
        {
            Assert.Equal(
                expectedUri.Host,
                span.GetTagValue(SemanticConventions.AttributeHttpHost) as string);
        }
        else
        {
            Assert.Equal(
                $"{expectedUri.Host}:{expectedUri.Port}",
                span.GetTagValue(SemanticConventions.AttributeHttpHost) as string);
        }

        Assert.Equal(HttpContext.Current.Request.HttpMethod, span.GetTagValue(SemanticConventions.AttributeHttpMethod) as string);
        Assert.Equal(HttpContext.Current.Request.Path, span.GetTagValue(SemanticConventions.AttributeHttpTarget) as string);
        Assert.Equal(HttpContext.Current.Request.UserAgent, span.GetTagValue(SemanticConventions.AttributeHttpUserAgent) as string);

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

                default:
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
