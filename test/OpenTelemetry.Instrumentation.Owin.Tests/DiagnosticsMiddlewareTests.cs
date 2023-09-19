// <copyright file="DiagnosticsMiddlewareTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using OpenTelemetry.Instrumentation.Owin.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Owin;
using Xunit;

namespace OpenTelemetry.Instrumentation.Owin.Tests;

public class DiagnosticsMiddlewareTests : IDisposable
{
    private readonly Uri serviceBaseUri;
    private readonly IDisposable listener;
    private readonly EventWaitHandle requestCompleteHandle = new(false, EventResetMode.AutoReset);

    public DiagnosticsMiddlewareTests()
    {
        Random random = new Random();
        var retryCount = 5;
        while (retryCount > 0)
        {
            try
            {
                this.serviceBaseUri = new Uri($"http://localhost:{random.Next(2000, 5000)}/");

                this.listener = WebApp.Start(
                    this.serviceBaseUri.ToString(),
                    appBuilder =>
                    {
                        appBuilder.Use((context, next) =>
                        {
                            try
                            {
                                return next();
                            }
                            finally
                            {
                                this.requestCompleteHandle?.Set();
                            }
                        });

                        appBuilder.UseOpenTelemetry();

                        appBuilder.Use((context, next) =>
                        {
                            if (context.Request.Path == new PathString("/exception"))
                            {
                                context.Response.StatusCode = 500;
                                throw new InvalidOperationException("Unhandled exception requested by caller.");
                            }

                            return next();
                        });

                        HttpConfiguration config = new HttpConfiguration();
                        config.Routes.MapHttpRoute(
                            name: "DefaultApi",
                            routeTemplate: "api/{controller}/{id}",
                            defaults: new { id = RouteParameter.Optional });

                        appBuilder.UseWebApi(config);
                    });
                break;
            }
            catch
            {
                this.listener?.Dispose();
                this.listener = null;
                retryCount--;
            }
        }

        if (this.listener == null)
        {
            throw new InvalidOperationException("HttpListener could not be started.");
        }
    }

    public void Dispose()
    {
        this.listener?.Dispose();
        this.requestCompleteHandle?.Dispose();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, false, true, true)]
    [InlineData(true, false, false, false, false, true)]
    [InlineData(true, false, false, false, false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, true, false, false, false, true)]
    [InlineData(true, true, false, false, false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, false, true, true)]
    [InlineData(false, true, false, false, false, true)]
    [InlineData(false, true, false, false, false, true, true)]
    [InlineData(false, false)]
    public async Task OutgoingRequestInstrumentationTest(
        bool instrumentTraces,
        bool instrumentMetrics,
        bool filter = false,
        bool enrich = false,
        bool enrichmentException = false,
        bool generateRemoteException = false,
        bool recordException = false)
    {
        List<Activity> stoppedActivities = new List<Activity>();
        List<Metric> exportedMetrics = new List<Metric>();

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(stoppedActivities);
        var meterBuilder = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(exportedMetrics);

        if (instrumentTraces)
        {
            builder
                .AddOwinInstrumentation(options =>
                {
                    if (enrich)
                    {
                        if (!enrichmentException)
                        {
                            options.Enrich = (activity, eventName, context, exception) =>
                            {
                                switch (eventName)
                                {
                                    case OwinEnrichEventType.BeginRequest:
                                        activity.SetTag("client.beginrequest", nameof(OwinEnrichEventType.BeginRequest));
                                        break;
                                    case OwinEnrichEventType.EndRequest:
                                        activity.SetTag("client.endrequest", nameof(OwinEnrichEventType.EndRequest));
                                        break;
                                }
                            };
                        }
                        else
                        {
                            options.Enrich = (activity, eventName, context, exception) => throw new Exception("Error while enriching activity");
                        }
                    }

                    options.Filter = _ => !filter;
                    options.RecordException = recordException;
                });
        }

        if (instrumentMetrics)
        {
            meterBuilder.AddOwinInstrumentation();
        }

        using TracerProvider tracerProvider = builder.Build();
        using MeterProvider meterProvider = meterBuilder.Build();

        using HttpClient client = new HttpClient();

        Uri requestUri = generateRemoteException
            ? new Uri($"{this.serviceBaseUri}exception")
            : new Uri($"{this.serviceBaseUri}api/test");

        this.requestCompleteHandle.Reset();

        using var response = await client.GetAsync(requestUri).ConfigureAwait(false);

        /* Note: This code will continue executing as soon as the response
        is available but Owin could still be working. We need to wait until
        Owin has finished to inspect the activity status. */

        Assert.True(this.requestCompleteHandle.WaitOne(3000));

        if (instrumentTraces)
        {
            if (!filter)
            {
                Assert.NotEmpty(stoppedActivities);
                Assert.Single(stoppedActivities);

                Activity activity = stoppedActivities[0];
                Assert.Equal(OwinInstrumentationActivitySource.IncomingRequestActivityName, activity.OperationName);

                Assert.Equal(requestUri.Host + ":" + requestUri.Port, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeHttpHost).Value);
                Assert.Equal("GET", activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeHttpMethod).Value);
                Assert.Equal(requestUri.AbsolutePath, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeHttpTarget).Value);
                Assert.Equal(requestUri.ToString(), activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeHttpUrl).Value);

                Assert.Equal(generateRemoteException ? 500 : 200, activity.TagObjects.FirstOrDefault(t => t.Key == SemanticConventions.AttributeHttpStatusCode).Value);
                if (generateRemoteException)
                {
                    Assert.Equal(Status.Error, activity.GetStatus());

                    if (recordException)
                    {
                        Assert.Contains(activity.Events, ae => ae.Name == SemanticConventions.AttributeExceptionEventName);
                    }
                }
                else
                {
                    Assert.Equal(Status.Unset, activity.GetStatus());
                }

                if (enrich && !enrichmentException)
                {
                    Assert.Equal(nameof(OwinEnrichEventType.BeginRequest), activity.TagObjects.Single(t => t.Key == "client.beginrequest").Value);
                    Assert.Equal(nameof(OwinEnrichEventType.EndRequest), activity.TagObjects.Single(t => t.Key == "client.endrequest").Value);
                }
            }
            else
            {
                Assert.Empty(stoppedActivities);
            }
        }
        else
        {
            Assert.Empty(stoppedActivities);
        }

        if (instrumentMetrics)
        {
            meterProvider.Dispose();
            var metric = exportedMetrics[0];
            var metricPoints = this.GetMetricPoints(metric);
            var metricPoint = Assert.Single(metricPoints);

            Assert.Equal(OwinInstrumentationMetrics.MeterName, metric.MeterName);
            Assert.Equal("http.server.request.duration", metric.Name);
            Assert.Equal(MetricType.Histogram, metric.MetricType);
            Assert.Equal(1, metricPoint.GetHistogramCount());
            Assert.Equal(3, metricPoint.Tags.Count);

            foreach (var tag in metricPoint.Tags)
            {
                switch (tag.Key)
                {
                    case SemanticConventions.AttributeHttpMethod:
                        Assert.Equal("GET", tag.Value);
                        break;
                    case SemanticConventions.AttributeHttpScheme:
                        Assert.Equal(requestUri.Scheme, tag.Value);
                        break;
                    case SemanticConventions.AttributeHttpStatusCode:
                        Assert.Equal(generateRemoteException ? 500 : 200, tag.Value);
                        break;
                }
            }
        }
        else
        {
            Assert.Empty(exportedMetrics);
        }

        if (instrumentMetrics && instrumentTraces && !filter)
        {
            var metric = Assert.Single(exportedMetrics);
            var activity = Assert.Single(stoppedActivities);
            var metricPoints = this.GetMetricPoints(metric);
            var metricPoint = Assert.Single(metricPoints);

            // metric value and span duration should match
            // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/http/http-metrics.md#metric-httpserverrequestduration
            Assert.Equal(activity.Duration.TotalSeconds, metricPoint.GetHistogramSum());
        }
    }

    private List<MetricPoint> GetMetricPoints(Metric metric)
    {
        List<MetricPoint> metricPoints = new();

        foreach (var metricPoint in metric.GetMetricPoints())
        {
            metricPoints.Add(metricPoint);
        }

        return metricPoints;
    }
}
