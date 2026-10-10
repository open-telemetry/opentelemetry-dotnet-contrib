// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using TestApp.AspNetCore;
using TestApp.AspNetCore.Filters;
using Uri = System.Uri;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

// See https://github.com/aspnet/Docs/tree/master/aspnetcore/test/integration-tests/samples/2.x/IntegrationTestsSample
[Collection("AspNetCore")]
public sealed class BasicTests
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private static readonly string ActivitySourceName = typeof(BasicTests).FullName!;

    private readonly WebApplicationFactory<Program> factory;
    private readonly List<WebApplicationFactory<Program>> derivedFactories = [];
    private TracerProvider? tracerProvider;

    public BasicTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void AddAspNetCoreInstrumentation_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddAspNetCoreInstrumentation());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusIsUnsetOn200Response(bool disableLogging)
    {
        var exportedItems = new List<Activity>();
        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                if (disableLogging)
                {
                    builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                }
            }))
        {
            // Act
            using var response = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299

            WaitForActivityExport(exportedItems, 1);
        }

        var activity = Assert.Single(exportedItems);

        Assert.Equal(200, activity.GetTagValue(SemanticConventions.AttributeHttpResponseStatusCode));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        ValidateAspNetCoreActivity(activity, "/api/values");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulTemplateControllerCallGeneratesASpan(bool shouldEnrich)
    {
        var exportedItems = new List<Activity>();
        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation(options =>
                {
                    if (shouldEnrich)
                    {
                        options.EnrichWithHttpRequest = (activity, request) => { activity.SetTag("enrichedOnStart", "yes"); };
                        options.EnrichWithHttpResponse = (activity, response) => { activity.SetTag("enrichedOnStop", "yes"); };
                    }
                })
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            // Act
            using var response = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299

            WaitForActivityExport(exportedItems, 1);
        }

        var activity = Assert.Single(exportedItems);

        if (shouldEnrich)
        {
            Assert.Contains(activity.Tags, tag => tag.Key == "enrichedOnStart" && tag.Value == "yes");
            Assert.Contains(activity.Tags, tag => tag.Key == "enrichedOnStop" && tag.Value == "yes");
        }

        ValidateAspNetCoreActivity(activity, "/api/values");
    }

    [Fact]
    public async Task SuccessfulTemplateControllerCallUsesParentContext()
    {
        var exportedItems = new List<Activity>();
        var expectedTraceId = ActivityTraceId.CreateRandom();
        var expectedSpanId = ActivitySpanId.CreateRandom();

        // Arrange
        using (var testFactory = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .AddInMemoryExporter(exportedItems)
                    .Build();
                });

                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var client = testFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/values/2");
            request.Headers.Add("traceparent", $"00-{expectedTraceId}-{expectedSpanId}-01");

            // Act
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299

            WaitForActivityExport(exportedItems, 1);
        }

        var activity = Assert.Single(exportedItems);

        Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", activity.OperationName);

        Assert.Equal(expectedTraceId, activity.Context.TraceId);
        Assert.Equal(expectedSpanId, activity.ParentSpanId);

        ValidateAspNetCoreActivity(activity, "/api/values/2");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomPropagator(bool addSampler)
    {
        try
        {
            var exportedItems = new List<Activity>();
            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedSpanId = ActivitySpanId.CreateRandom();

            var propagator = new CustomTextMapPropagator
            {
                TraceId = expectedTraceId,
                SpanId = expectedSpanId,
            };

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    {
                        builder.ConfigureTestServices(services =>
                        {
                            Sdk.SetDefaultTextMapPropagator(propagator);
                            var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();

                            if (addSampler)
                            {
                                tracerProviderBuilder
                                    .SetSampler(new TestSampler(SamplingDecision.RecordAndSample, new Dictionary<string, object> { { "SomeTag", "SomeKey" }, }));
                            }

                            this.tracerProvider = tracerProviderBuilder
                                                    .AddAspNetCoreInstrumentation()
                                                    .AddInMemoryExporter(exportedItems)
                                                    .Build();
                        });
                        builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                    }))
            {
                using var client = testFactory.CreateClient();
                using var response = await client.GetAsync(new Uri("/api/values/2", UriKind.Relative), TestContext.Current.CancellationToken);
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForActivityExport(exportedItems, 1);
            }

            var activity = Assert.Single(exportedItems);

            Assert.True(activity.Duration != TimeSpan.Zero);

            Assert.Equal(expectedTraceId, activity.Context.TraceId);
            Assert.Equal(expectedSpanId, activity.ParentSpanId);

            ValidateAspNetCoreActivity(activity, "/api/values/2");
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        }
    }

    [Fact]
    public async Task RequestNotCollectedWhenFilterIsApplied()
    {
        var exportedItems = new List<Activity>();

        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation((opt) => opt.Filter = (ctx) => ctx.Request.Path != "/api/values/2")
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using (var testFactory = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var client = testFactory.CreateClient();

            // Act
            using var response1 = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);
            using var response2 = await client.GetAsync(new Uri("/api/values/2", UriKind.Relative), TestContext.Current.CancellationToken);

            // Assert
            response1.EnsureSuccessStatusCode(); // Status Code 200-299
            response2.EnsureSuccessStatusCode(); // Status Code 200-299

            WaitForActivityExport(exportedItems, 1);
        }

        var activity = Assert.Single(exportedItems);

        ValidateAspNetCoreActivity(activity, "/api/values");
    }

    [Fact]
    public async Task RequestNotCollectedWhenFilterThrowException()
    {
        var exportedItems = new List<Activity>();

        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation((opt) => opt.Filter = (ctx) =>
                {
                    return ctx.Request.Path == "/api/values/2"
                        ? throw new Exception("from InstrumentationFilter")
                        : true;
                })
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using (var testFactory = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var client = testFactory.CreateClient();

            // Act
            using (var inMemoryEventListener = new InMemoryEventListener(AspNetCoreInstrumentationEventSource.Log))
            {
                using var response1 = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);
                using var response2 = await client.GetAsync(new Uri("/api/values/2", UriKind.Relative), TestContext.Current.CancellationToken);

                response1.EnsureSuccessStatusCode(); // Status Code 200-299
                response2.EnsureSuccessStatusCode(); // Status Code 200-299
                Assert.Single(inMemoryEventListener.Events, e => e.EventId == 3);
            }

            WaitForActivityExport(exportedItems, 1);
        }

        // As InstrumentationFilter threw, we continue as if the
        // InstrumentationFilter did not exist.

        var activity = Assert.Single(exportedItems);
        ValidateAspNetCoreActivity(activity, "/api/values");
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public async Task ExtractContextIrrespectiveOfSamplingDecision(SamplingDecision samplingDecision)
    {
        try
        {
            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedParentSpanId = ActivitySpanId.CreateRandom();
            var expectedTraceState = "rojo=1,congo=2";
            var activityContext = new ActivityContext(expectedTraceId, expectedParentSpanId, ActivityTraceFlags.Recorded, expectedTraceState, true);
            var expectedBaggage = Baggage.SetBaggage("key1", "value1").SetBaggage("key2", "value2");
            Sdk.SetDefaultTextMapPropagator(new ExtractOnlyPropagator(activityContext, expectedBaggage));

            // Arrange
            using var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    {
                        builder.ConfigureTestServices(services => { this.tracerProvider = Sdk.CreateTracerProviderBuilder().SetSampler(new TestSampler(samplingDecision)).AddAspNetCoreInstrumentation().Build(); });
                        builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                    });
            using var client = testFactory.CreateClient();

            // Test TraceContext Propagation
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityTraceContext");
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var childActivityTraceContext = JsonSerializer.Deserialize<Dictionary<string, string>>(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            response.EnsureSuccessStatusCode();

            Assert.NotNull(childActivityTraceContext);
            Assert.Equal(expectedTraceId.ToString(), childActivityTraceContext["TraceId"]);
            Assert.Equal(expectedTraceState, childActivityTraceContext["TraceState"]);
            Assert.NotEqual(expectedParentSpanId.ToString(), childActivityTraceContext["ParentSpanId"]); // there is a new activity created in instrumentation therefore the ParentSpanId is different that what is provided in the headers

            // Test Baggage Context Propagation
            request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityBaggageContext");

            response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var childActivityBaggageContext = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            response.EnsureSuccessStatusCode();

            Assert.NotNull(childActivityBaggageContext);
            Assert.Single(childActivityBaggageContext, item => item.Key == "key1" && item.Value == "value1");
            Assert.Single(childActivityBaggageContext, item => item.Key == "key2" && item.Value == "value2");
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        }
    }

    [Fact]
    public async Task ExtractContextIrrespectiveOfTheFilterApplied()
    {
        try
        {
            var expectedTraceId = ActivityTraceId.CreateRandom();
            var expectedParentSpanId = ActivitySpanId.CreateRandom();
            var expectedTraceState = "rojo=1,congo=2";
            var activityContext = new ActivityContext(expectedTraceId, expectedParentSpanId, ActivityTraceFlags.Recorded, expectedTraceState);
            var expectedBaggage = Baggage.SetBaggage("key1", "value1").SetBaggage("key2", "value2");
            Sdk.SetDefaultTextMapPropagator(new ExtractOnlyPropagator(activityContext, expectedBaggage));

            // Arrange
            var isFilterCalled = false;
            using var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                            .AddAspNetCoreInstrumentation(options =>
                            {
                                options.Filter = context =>
                                {
                                    isFilterCalled = true;
                                    return false;
                                };
                            })
                            .Build();
                    });
                    builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                });
            using var client = testFactory.CreateClient();

            // Test TraceContext Propagation
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityTraceContext");
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // Ensure that filter was called
            Assert.True(isFilterCalled);

            var childActivityTraceContext = JsonSerializer.Deserialize<Dictionary<string, string>>(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            response.EnsureSuccessStatusCode();

            Assert.NotNull(childActivityTraceContext);
            Assert.Equal(expectedTraceId.ToString(), childActivityTraceContext["TraceId"]);
            Assert.Equal(expectedTraceState, childActivityTraceContext["TraceState"]);
            Assert.NotEqual(expectedParentSpanId.ToString(), childActivityTraceContext["ParentSpanId"]); // there is a new activity created in instrumentation therefore the ParentSpanId is different that what is provided in the headers

            // Test Baggage Context Propagation
            request = new HttpRequestMessage(HttpMethod.Get, "/api/GetChildActivityBaggageContext");

            response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var childActivityBaggageContext = JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            response.EnsureSuccessStatusCode();

            Assert.NotNull(childActivityBaggageContext);
            Assert.Single(childActivityBaggageContext, item => item.Key == "key1" && item.Value == "value1");
            Assert.Single(childActivityBaggageContext, item => item.Key == "key2" && item.Value == "value2");
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        }
    }

    [Fact]
    public async Task BaggageIsNotClearedWhenActivityStopped()
    {
        int? baggageCountAfterStart = null;
        int? baggageCountAfterStop = null;
        using var stopSignal = new EventWaitHandle(false, EventResetMode.ManualReset);

        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation(
                    new TestHttpInListener(new AspNetCoreTraceInstrumentationOptions())
                    {
                        OnEventWrittenCallback = (name, payload) =>
                        {
                            switch (name)
                            {
                                case HttpInListener.OnStartEvent:
                                    {
                                        baggageCountAfterStart = Baggage.Current.Count;
                                    }

                                    break;
                                case HttpInListener.OnStopEvent:
                                    {
                                        baggageCountAfterStop = Baggage.Current.Count;
                                        stopSignal.Set();
                                    }

                                    break;
                                default:
                                    break;
                            }
                        },
                    })
                .Build();
        }

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/values");

            request.Headers.TryAddWithoutValidation("baggage", "TestKey1=123,TestKey2=456");

            // Act
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        stopSignal.WaitOne(5000);

        // Assert
        Assert.NotNull(baggageCountAfterStart);
        Assert.Equal(2, baggageCountAfterStart);
        Assert.NotNull(baggageCountAfterStop);
        Assert.Equal(2, baggageCountAfterStop);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop, false, false)]
    [InlineData(SamplingDecision.RecordOnly, true, true)]
    [InlineData(SamplingDecision.RecordAndSample, true, true)]
    public async Task FilterAndEnrichAreOnlyCalledWhenSampled(SamplingDecision samplingDecision, bool shouldFilterBeCalled, bool shouldEnrichBeCalled)
    {
        var filterCalled = false;
        var enrichWithHttpRequestCalled = false;
        var enrichWithHttpResponseCalled = false;
        using var stopSignal = new EventWaitHandle(false, EventResetMode.ManualReset);

        void ConfigureTestServices(IServiceCollection services)
        {
            var options = new AspNetCoreTraceInstrumentationOptions
            {
                Filter = (context) =>
                {
                    filterCalled = true;
                    return true;
                },
                EnrichWithHttpRequest = (activity, request) =>
                {
                    enrichWithHttpRequestCalled = true;
                },
                EnrichWithHttpResponse = (activity, request) =>
                {
                    enrichWithHttpResponseCalled = true;
                },
            };

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new TestSampler(samplingDecision))
                .AddAspNetCoreInstrumentation(
                    new TestHttpInListener(options)
                    {
                        OnEventWrittenCallback = (name, payload) =>
                        {
                            if (name == HttpInListener.OnStopEvent)
                            {
                                stopSignal.Set();
                            }
                        },
                    })
                .Build();
        }

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            // Act
            using var response = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);
        }

        Assert.True(stopSignal.WaitOne(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.Equal(shouldFilterBeCalled, filterCalled);
        Assert.Equal(shouldEnrichBeCalled, enrichWithHttpRequestCalled);
        Assert.Equal(shouldEnrichBeCalled, enrichWithHttpResponseCalled);
    }

    [Fact]
    public async Task ActivitiesStartedInMiddlewareShouldNotBeUpdated()
    {
        var exportedItems = new List<Activity>();

        var activitySourceName = "TestMiddlewareActivitySource";
        var activityName = "TestMiddlewareActivity";

        void ConfigureTestServices(IServiceCollection services)
        {
            services.AddSingleton<TestActivityMiddleware>(new TestTestActivityMiddleware(activitySourceName, activityName));
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddSource(activitySourceName)
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var response = await client.GetAsync(new Uri("/api/values/2", UriKind.Relative), TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            WaitForActivityExport(exportedItems, 2);
        }

        Assert.Equal(2, exportedItems.Count);

        var middlewareActivity = exportedItems[0];

        var aspnetcoreframeworkactivity = exportedItems[1];

        // Middleware activity name should not be changed
        Assert.Equal(ActivityKind.Internal, middlewareActivity.Kind);
        Assert.Equal(activityName, middlewareActivity.OperationName);
        Assert.Equal(activityName, middlewareActivity.DisplayName);

        // tag http.method should be added on activity started by asp.net core
        Assert.Equal("GET", aspnetcoreframeworkactivity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod) as string);
        Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", aspnetcoreframeworkactivity.OperationName);
    }

    [Theory]
    [InlineData("CONNECT", "CONNECT", null, "CONNECT")]
    [InlineData("DELETE", "DELETE", null, "DELETE")]
    [InlineData("GET", "GET", null, "GET")]
    [InlineData("PUT", "PUT", null, "PUT")]
    [InlineData("HEAD", "HEAD", null, "HEAD")]
    [InlineData("OPTIONS", "OPTIONS", null, "OPTIONS")]
    [InlineData("PATCH", "PATCH", null, "PATCH")]
    [InlineData("Get", "GET", "Get", "GET")]
    [InlineData("POST", "POST", null, "POST")]
    [InlineData("TRACE", "TRACE", null, "TRACE")]
    [InlineData("CUSTOM", "_OTHER", "CUSTOM", "HTTP")]
    public async Task HttpRequestMethodAndActivityDisplayIsSetAsPerSpec(string originalMethod, string expectedMethod, string? expectedOriginalMethod, string expectedDisplayName)
    {
        var exportedItems = new List<Activity>();

        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            });

        var message = new HttpRequestMessage
        {
            Method = new HttpMethod(originalMethod),
        };

        try
        {
            using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            // ignore error.
        }

        WaitForActivityExport(exportedItems, 1);

        var activity = Assert.Single(exportedItems);

        Assert.Equal(expectedMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod));
        Assert.Equal(expectedOriginalMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethodOriginal));
        Assert.Equal(expectedDisplayName, activity.DisplayName);
    }

    [Fact]
    public async Task ActivitiesStartedInMiddlewareBySettingHostActivityToNullShouldNotBeUpdated()
    {
        var exportedItems = new List<Activity>();

        var activitySourceName = "TestMiddlewareActivitySource";
        var activityName = "TestMiddlewareActivity";

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<TestActivityMiddleware>(new TestNullHostActivityMiddlewareImpl(activitySourceName, activityName));
                    services.AddOpenTelemetry()
                        .WithTracing(builder => builder
                            .AddAspNetCoreInstrumentation()
                            .AddSource(activitySourceName)
                            .AddInMemoryExporter(exportedItems));
                });
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var response = await client.GetAsync(new Uri("/api/values/2", UriKind.Relative), TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            WaitForActivityExport(exportedItems, 2);
        }

        Assert.Equal(2, exportedItems.Count);

        var middlewareActivity = exportedItems[0];

        var aspnetcoreframeworkactivity = exportedItems[1];

        // Middleware activity name should not be changed
        Assert.Equal(ActivityKind.Internal, middlewareActivity.Kind);
        Assert.Equal(activityName, middlewareActivity.OperationName);
        Assert.Equal(activityName, middlewareActivity.DisplayName);

        // tag http.method should be added on activity started by asp.net core
        Assert.Equal("GET", aspnetcoreframeworkactivity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod) as string);
        Assert.Equal("Microsoft.AspNetCore.Hosting.HttpRequestIn", aspnetcoreframeworkactivity.OperationName);
    }

    [Fact]
    public async Task UserRegisteredActivitySourceIsUsedForActivityCreationByAspNetCore()
    {
        var exportedItems = new List<Activity>();
        void ConfigureTestServices(IServiceCollection services)
        {
            services.AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddInMemoryExporter(exportedItems));

            // Register ActivitySource here so that it will be used
            // by ASP.NET Core to create activities
            // https://github.com/dotnet/aspnetcore/blob/0e5cbf447d329a1e7d69932c3decd1c70a00fbba/src/Hosting/Hosting/src/Internal/WebHost.cs#L152
            services.AddSingleton(sp => new ActivitySource("UserRegisteredActivitySource"));
        }

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            // Act
            using var response = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299

            WaitForActivityExport(exportedItems, 1);
        }

        var activity = Assert.Single(exportedItems);

        Assert.Equal("UserRegisteredActivitySource", activity.Source.Name);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ShouldExportActivityWithOneOrMoreExceptionFilters(int mode)
    {
        var exportedItems = new List<Activity>();

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(
                (s) => this.ConfigureExceptionFilters(s, mode, ref exportedItems));
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            // Act
            using var response = await client.GetAsync(new Uri("/api/error", UriKind.Relative), TestContext.Current.CancellationToken);

            WaitForActivityExport(exportedItems, 1);
        }

        // Assert
        AssertException(exportedItems);
    }

    [Fact]
    public async Task DiagnosticSourceCallbacksAreReceivedOnlyForSubscribedEvents()
    {
        var numberOfUnSubscribedEvents = 0;
        var numberofSubscribedEvents = 0;

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation(
                new TestHttpInListener(new AspNetCoreTraceInstrumentationOptions())
                {
                    OnEventWrittenCallback = (name, payload) =>
                    {
                        switch (name)
                        {
                            case HttpInListener.OnStartEvent:
                                {
                                    numberofSubscribedEvents++;
                                }

                                break;
                            case HttpInListener.OnStopEvent:
                                {
                                    numberofSubscribedEvents++;
                                }

                                break;
                            default:
                                {
                                    numberOfUnSubscribedEvents++;
                                }

                                break;
                        }
                    },
                })
            .Build();

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/values");

            // Act
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        WaitForEventCount(() => numberofSubscribedEvents, 2);

        Assert.Equal(0, numberOfUnSubscribedEvents);
        Assert.Equal(2, numberofSubscribedEvents);
    }

    [Fact]
    public async Task DiagnosticSourceExceptionCallbackIsReceivedForUnHandledException()
    {
        var numberOfUnSubscribedEvents = 0;
        var numberofSubscribedEvents = 0;
        var numberOfExceptionCallbacks = 0;

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation(
                new TestHttpInListener(new AspNetCoreTraceInstrumentationOptions())
                {
                    OnEventWrittenCallback = (name, payload) =>
                    {
                        switch (name)
                        {
                            case HttpInListener.OnStartEvent:
                                {
                                    numberofSubscribedEvents++;
                                }

                                break;
                            case HttpInListener.OnStopEvent:
                                {
                                    numberofSubscribedEvents++;
                                }

                                break;

                            // TODO: Add test case for validating name for both the types
                            // of exception event.
                            case HttpInListener.OnUnhandledHostingExceptionEvent:
                            case HttpInListener.OnUnHandledDiagnosticsExceptionEvent:
                                {
                                    numberofSubscribedEvents++;
                                    numberOfExceptionCallbacks++;
                                }

                                break;
                            default:
                                {
                                    numberOfUnSubscribedEvents++;
                                }

                                break;
                        }
                    },
                })
            .Build();

        // Arrange
        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/error");

                // Act
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            }
            catch
            {
                // ignore exception
            }
        }

        WaitForEventCount(() => numberofSubscribedEvents, 3);

        Assert.Equal(1, numberOfExceptionCallbacks);
        Assert.Equal(0, numberOfUnSubscribedEvents);
        Assert.Equal(3, numberofSubscribedEvents);
    }

    [Fact]
    public async Task DiagnosticSourceExceptionCallBackIsNotReceivedForExceptionsHandledInMiddleware()
    {
        var numberOfUnSubscribedEvents = 0;
        var numberOfSubscribedEvents = 0;
        var numberOfExceptionCallbacks = 0;
        var exceptionHandled = false;

        // configure SDK
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation(
                new TestHttpInListener(new AspNetCoreTraceInstrumentationOptions())
                {
                    OnEventWrittenCallback = (name, payload) =>
                    {
                        switch (name)
                        {
                            case HttpInListener.OnStartEvent:
                                {
                                    numberOfSubscribedEvents++;
                                }

                                break;
                            case HttpInListener.OnStopEvent:
                                {
                                    numberOfSubscribedEvents++;
                                }

                                break;

                            // TODO: Add test case for validating name for both the types
                            // of exception event.
                            case HttpInListener.OnUnhandledHostingExceptionEvent:
                            case HttpInListener.OnUnHandledDiagnosticsExceptionEvent:
                                {
                                    numberOfSubscribedEvents++;
                                    numberOfExceptionCallbacks++;
                                }

                                break;
                            default:
                                {
                                    numberOfUnSubscribedEvents++;
                                }

                                break;
                        }
                    },
                })
                .Build();

        TestMiddleware.Create(builder => builder
            .UseExceptionHandler(handler =>
                handler.Run(async (ctx) =>
                {
                    exceptionHandled = true;
                    await ctx.Response.WriteAsync("handled");
                })));

        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/error");
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            }
            catch
            {
                // ignore exception
            }
        }

        WaitForEventCount(() => numberOfSubscribedEvents, 2);

        Assert.Equal(0, numberOfExceptionCallbacks);
        Assert.Equal(0, numberOfUnSubscribedEvents);
        Assert.Equal(2, numberOfSubscribedEvents);
        Assert.True(exceptionHandled);
    }

    [Fact]
    public async Task NoSiblingActivityCreatedWhenTraceFlagsNone()
    {
        using var localTracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddAspNetCoreInstrumentation()
            .Build();

        using var testFactory = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .Build();
                });

                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            });
        using var client = testFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/GetActivityEquality");
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        request.Headers.Add("traceparent", $"00-{traceId}-{spanId}-00");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var result = bool.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.True(response.IsSuccessStatusCode);

        // Confirm that Activity.Current and IHttpActivityFeature activity are same
        Assert.True(result);
    }

    [Theory]
    [InlineData("?a", "?a", false)]
    [InlineData("?a=bdjdjh", "?a=Redacted", false)]
    [InlineData("?a=b&", "?a=Redacted&", false)]
    [InlineData("?c=b&", "?c=Redacted&", false)]
    [InlineData("?c=a", "?c=Redacted", false)]
    [InlineData("?a=b&c", "?a=Redacted&c", false)]
    [InlineData("?a=b&c=1123456&", "?a=Redacted&c=Redacted&", false)]
    [InlineData("?a=b&c=1&a1", "?a=Redacted&c=Redacted&a1", false)]
    [InlineData("?a=ghgjgj&c=1deedd&a1=", "?a=Redacted&c=Redacted&a1=Redacted", false)]
    [InlineData("?a=b&c=11&a1=&", "?a=Redacted&c=Redacted&a1=Redacted&", false)]
    [InlineData("?c&c&c&", "?c&c&c&", false)]
    [InlineData("?a&a&a&a", "?a&a&a&a", false)]
    [InlineData("?&&&&&&&", "?&&&&&&&", false)]
    [InlineData("?c", "?c", false)]
    [InlineData("?a", "?a", true)]
    [InlineData("?a=bdfdfdf", "?a=bdfdfdf", true)]
    [InlineData("?a=b&", "?a=b&", true)]
    [InlineData("?c=b&", "?c=b&", true)]
    [InlineData("?c=a", "?c=a", true)]
    [InlineData("?a=b&c", "?a=b&c", true)]
    [InlineData("?a=b&c=111111&", "?a=b&c=111111&", true)]
    [InlineData("?a=b&c=1&a1", "?a=b&c=1&a1", true)]
    [InlineData("?a=b&c=1&a1=", "?a=b&c=1&a1=", true)]
    [InlineData("?a=b123&c=11&a1=&", "?a=b123&c=11&a1=&", true)]
    [InlineData("?c&c&c&", "?c&c&c&", true)]
    [InlineData("?a&a&a&a", "?a&a&a&a", true)]
    [InlineData("?&&&&&&&", "?&&&&&&&", true)]
    [InlineData("?c", "?c", true)]
    [InlineData("?c=%26&", "?c=Redacted&", false)]
    public async Task ValidateUrlQueryRedaction(string urlQuery, string expectedUrlQuery, bool disableQueryRedaction)
    {
        var exportedItems = new List<Activity>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION"] = disableQueryRedaction.ToString() })
            .Build();

        var path = "/api/values" + urlQuery;

        // Arrange
        using var traceprovider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            try
            {
                using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
            }
            catch (Exception)
            {
                // ignore errors
            }

            WaitForActivityExport(exportedItems, 1);
        }

        var activity = Assert.Single(exportedItems);

        Assert.Equal(expectedUrlQuery, activity.GetTagValue(SemanticConventions.AttributeUrlQuery));
    }

    [Fact]
    public async Task NewRootSpanIsStartedWhenEnabledViaConfiguration()
    {
        var exportedItems = new List<Activity>();
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_NEW_ROOT_SPAN"] = "true" })
            .Build();

        // Arrange
        using var traceprovider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/values");
            request.Headers.Add("traceparent", $"00-{traceId}-{spanId}-01");

            // Act
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // Assert
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        var activity = Assert.Single(exportedItems);

        Assert.Null(activity.ParentId);
        Assert.NotEqual(traceId, activity.TraceId);

        var link = Assert.Single(activity.Links);
        Assert.Equal(traceId, link.Context.TraceId);
        Assert.Equal(spanId, link.Context.SpanId);

        ValidateAspNetCoreActivity(activity, "/api/values");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RootSpanIsStartedWhenRequestHasRemoteParent(bool traceContextPropagatorOnly)
    {
        try
        {
            if (traceContextPropagatorOnly)
            {
                Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
            }

            // Arrange
            var exportedItems = new List<Activity>();
            using var tracerProvider = CreateTracerProvider(exportedItems);
            using var source = new ActivitySource(ActivitySourceName);

            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var context = CreateContext();
            context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

            var listener = CreateListener();
            using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

            // Act
            listener.OnStartActivity(frameworkActivity, context);
            listener.OnStopActivity(Activity.Current!, context);
            frameworkActivity.Stop();

            // Assert
            var activity = Assert.Single(exportedItems);

            Assert.Null(activity.ParentId);
            Assert.NotEqual(traceId, activity.TraceId);
            Assert.Equal(ActivityKind.Server, activity.Kind);

            var link = Assert.Single(activity.Links);
            Assert.Equal(traceId, link.Context.TraceId);
            Assert.Equal(spanId, link.Context.SpanId);
            Assert.Equal(ActivityTraceFlags.Recorded, link.Context.TraceFlags);
            Assert.True(link.Context.IsRemote);
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RootSpanIsNotParentedToContextExtractedByPropagator(bool traceContextPropagatorOnly)
    {
        try
        {
            if (traceContextPropagatorOnly)
            {
                Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
            }

            // Arrange
            var exportedItems = new List<Activity>();
            using var tracerProvider = CreateTracerProvider(exportedItems);
            using var source = new ActivitySource(ActivitySourceName);

            var frameworkParent = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
            var extractedTraceId = ActivityTraceId.CreateRandom();
            var extractedSpanId = ActivitySpanId.CreateRandom();
            var context = CreateContext();
            context.Request.Headers["traceparent"] = $"00-{extractedTraceId}-{extractedSpanId}-01";

            var listener = CreateListener();
            using var frameworkActivity = StartFrameworkActivity(source, frameworkParent);

            // Act
            listener.OnStartActivity(frameworkActivity, context);
            listener.OnStopActivity(Activity.Current!, context);

            var currentAfterStop = Activity.Current;

            frameworkActivity.Stop();

            // Assert
            var activity = Assert.Single(exportedItems);

            Assert.Null(activity.ParentId);
            Assert.NotEqual(extractedTraceId, activity.TraceId);
            Assert.NotEqual(frameworkParent.TraceId, activity.TraceId);

            var link = Assert.Single(activity.Links);
            Assert.Equal(extractedTraceId, link.Context.TraceId);
            Assert.Equal(extractedSpanId, link.Context.SpanId);

            Assert.Same(frameworkActivity, currentAfterStop);
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        }
    }

    [Fact]
    public void RootSpanDoesNotCarryIncomingTraceState()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var traceState = "rojo=1,congo=2";
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";
        context.Request.Headers["tracestate"] = traceState;

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, traceState, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);
        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        var activity = Assert.Single(exportedItems);

        Assert.Null(activity.ParentId);
        Assert.Null(activity.TraceStateString);

        var link = Assert.Single(activity.Links);
        Assert.Equal(traceState, link.Context.TraceState);
    }

    [Fact]
    public void NoRootSpanIsStartedWhenRequestHasNoParent()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var context = CreateContext();

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source);

        // Act
        listener.OnStartActivity(frameworkActivity, context);

        var current = Activity.Current;

        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        Assert.Same(frameworkActivity, current);

        var activity = Assert.Single(exportedItems);

        Assert.Same(frameworkActivity, activity);
        Assert.Empty(activity.Links);
    }

    [Fact]
    public void RootSpanIsStartedWhenRequestHasLocalParent()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var context = CreateContext();

        var listener = CreateListener();
        using var parent = new Activity("parent") { ActivityTraceFlags = ActivityTraceFlags.Recorded }.Start();
        using var frameworkActivity = StartFrameworkActivity(source);

        // Act
        listener.OnStartActivity(frameworkActivity, context);
        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        var activity = Assert.Single(exportedItems);

        Assert.Null(activity.ParentId);
        Assert.NotEqual(parent.TraceId, activity.TraceId);

        var link = Assert.Single(activity.Links);
        Assert.Equal(parent.TraceId, link.Context.TraceId);
        Assert.Equal(parent.SpanId, link.Context.SpanId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RootSpanIsStartedWhenOnlyPropagatorExtractsParent(bool traceContextPropagatorOnly)
    {
        try
        {
            if (traceContextPropagatorOnly)
            {
                Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
            }

            // Arrange
            var exportedItems = new List<Activity>();
            using var tracerProvider = CreateTracerProvider(exportedItems);
            using var source = new ActivitySource(ActivitySourceName);

            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var context = CreateContext();
            context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

            var listener = CreateListener();

            using var frameworkActivity = StartFrameworkActivity(source);

            // Act
            listener.OnStartActivity(frameworkActivity, context);
            listener.OnStopActivity(Activity.Current!, context);
            frameworkActivity.Stop();

            // Assert
            var activity = Assert.Single(exportedItems);

            Assert.Null(activity.ParentId);
            Assert.NotEqual(traceId, activity.TraceId);

            var link = Assert.Single(activity.Links);
            Assert.Equal(traceId, link.Context.TraceId);
            Assert.Equal(spanId, link.Context.SpanId);
            Assert.Equal(ActivityTraceFlags.Recorded, link.Context.TraceFlags);
            Assert.True(link.Context.IsRemote);
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        }
    }

    [Fact]
    public void RootSpanIsStartedWhenOnlyFrameworkExtractsParent()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);
        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        var activity = Assert.Single(exportedItems);

        Assert.Null(activity.ParentId);
        Assert.NotEqual(traceId, activity.TraceId);

        var link = Assert.Single(activity.Links);
        Assert.Equal(traceId, link.Context.TraceId);
        Assert.Equal(spanId, link.Context.SpanId);
        Assert.Equal(ActivityTraceFlags.Recorded, link.Context.TraceFlags);
        Assert.True(link.Context.IsRemote);
    }

    [Fact]
    public void RootSpanIsSampledAsRootWhenIncomingParentIsNotSampled()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-00";

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.None, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);
        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        var activity = Assert.Single(exportedItems);

        Assert.Null(activity.ParentId);
        Assert.True(activity.Recorded);
    }

    [Fact]
    public void NoSpanIsExportedWhenSamplerDropsRootSpan()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ActivitySourceName)
            .SetSampler(new ParentBasedSampler(new AlwaysOffSampler()))
            .AddInMemoryExporter(exportedItems)
            .Build();
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);

        var root = Activity.Current;

        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        Assert.True(frameworkActivity.Recorded);

        Assert.NotNull(root);
        Assert.NotSame(frameworkActivity, root);
        Assert.Null(root.ParentId);
        Assert.NotEqual(traceId, root.TraceId);
        Assert.False(root.Recorded);

        Assert.Empty(exportedItems);
    }

    [Fact]
    public void ActivityCurrentIsRestoredToFrameworkActivityWhenRootSpanStops()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);

        var root = Activity.Current;

        listener.OnStopActivity(Activity.Current!, context);

        var currentAfterStop = Activity.Current;

        frameworkActivity.Stop();

        // Assert
        Assert.NotNull(root);
        Assert.Null(root.ParentId);
        Assert.Same(frameworkActivity, currentAfterStop);
    }

    [Fact]
    public void BaggageIsKeptWhenRootSpanIsStarted()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";
        context.Request.Headers["baggage"] = "key1=value1";

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));
        frameworkActivity.AddBaggage("key2", "value2");

        // Act
        listener.OnStartActivity(frameworkActivity, context);

        var root = Activity.Current;
        var baggage = Baggage.Current;

        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        Assert.NotNull(root);
        Assert.Null(root.ParentId);
        Assert.Equal("value1", baggage.GetBaggage("key1"));
        Assert.Equal("value2", root.GetBaggageItem("key2"));
    }

    [Fact]
    public void AmbientBaggageIsKeptWhenPropagatorIsTraceContextPropagator()
    {
        try
        {
            Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

            // Arrange
            var exportedItems = new List<Activity>();
            using var tracerProvider = CreateTracerProvider(exportedItems);
            using var source = new ActivitySource(ActivitySourceName);

            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var context = CreateContext();
            context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

            var listener = CreateListener();
            using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

            Baggage.SetBaggage("key1", "value1");

            // Act
            listener.OnStartActivity(frameworkActivity, context);

            var root = Activity.Current;
            var baggage = Baggage.Current;

            listener.OnStopActivity(Activity.Current!, context);
            frameworkActivity.Stop();

            // Assert
            Assert.NotNull(root);
            Assert.Null(root.ParentId);
            Assert.Equal("value1", baggage.GetBaggage("key1"));
        }
        finally
        {
            Baggage.Current = default;
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));
        }
    }

    [Fact]
    public void FilterIsAppliedToRootSpan()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

        var listener = CreateListener(options => options.Filter = _ => false);
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);

        var root = Activity.Current;

        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        Assert.NotSame(frameworkActivity, root);
        Assert.Empty(exportedItems);
    }

    [Fact]
    public void EnrichWithHttpRequestReceivesRootSpan()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

        Activity? enriched = null;
        var listener = CreateListener(options => options.EnrichWithHttpRequest = (activity, request) => enriched = activity);
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);

        var root = Activity.Current;

        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        Assert.NotNull(enriched);
        Assert.Same(root, enriched);
        Assert.Null(enriched.ParentId);
    }

    [Fact]
    public void RootSpanHasHttpTagsWhenAspNetCoreEmitsNativeTags()
    {
        const string SwitchName = "Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData";

        var wasConfigured = AppContext.TryGetSwitch(SwitchName, out var originalValue);
        AppContext.SetSwitch(SwitchName, false);

        try
        {
            // Arrange
            var exportedItems = new List<Activity>();
            using var tracerProvider = CreateTracerProvider(exportedItems);
            using var source = new ActivitySource(ActivitySourceName);

            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var context = CreateContext();
            context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

            var listener = CreateListener();
            using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

            // Act
            listener.OnStartActivity(frameworkActivity, context);
            listener.OnStopActivity(Activity.Current!, context);
            frameworkActivity.Stop();

            // Assert
            var activity = Assert.Single(exportedItems);

            Assert.Null(activity.ParentId);
            Assert.Equal("GET", activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod));
            Assert.Equal("localhost", activity.GetTagValue(SemanticConventions.AttributeServerAddress));
            Assert.Equal("http", activity.GetTagValue(SemanticConventions.AttributeUrlScheme));
            Assert.Equal("/api/values", activity.GetTagValue(SemanticConventions.AttributeUrlPath));
        }
        finally
        {
            var resetValue = wasConfigured ? originalValue : Environment.Version.Major < 11;
            AppContext.SetSwitch(SwitchName, resetValue);
        }
    }

    [Fact]
    public void RootSpanIsCreatedWithSamplingRelevantTags()
    {
        // Arrange
        var sampler = new OpenTelemetry.Tests.TestSampler();
        var exportedItems = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ActivitySourceName)
            .SetSampler(sampler)
            .AddInMemoryExporter(exportedItems)
            .Build();
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:72.0) Gecko/20100101 Firefox/72.0";
        var context = CreateContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com", 8080);
        context.Request.Path = "/webshop/articles/4";
        context.Request.QueryString = new QueryString("?s=1&t=2");
        context.Request.Headers["User-Agent"] = userAgent;
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);

        var root = Activity.Current;
        var samplingParameters = sampler.LatestSamplingParameters;

        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        Assert.NotNull(root);
        Assert.Null(root.ParentId);
        Assert.Equal(root.TraceId, samplingParameters.TraceId);
        Assert.NotNull(samplingParameters.Tags);

        var tags = samplingParameters.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);

        Assert.Equal("GET", Assert.Contains(SemanticConventions.AttributeHttpRequestMethod, tags));
        Assert.Equal("/webshop/articles/4", Assert.Contains(SemanticConventions.AttributeUrlPath, tags));
        Assert.Equal("https", Assert.Contains(SemanticConventions.AttributeUrlScheme, tags));
        Assert.Equal("example.com", Assert.Contains(SemanticConventions.AttributeServerAddress, tags));
        Assert.Equal(8080, Assert.Contains(SemanticConventions.AttributeServerPort, tags));
        Assert.Equal("?s=Redacted&t=Redacted", Assert.Contains(SemanticConventions.AttributeUrlQuery, tags));
        Assert.Equal(userAgent, Assert.Contains(SemanticConventions.AttributeUserAgentOriginal, tags));

        var activity = Assert.Single(exportedItems);

        foreach (var tag in tags)
        {
            Assert.Equal(tag.Value, activity.GetTagItem(tag.Key));
        }
    }

    [Fact]
    public void GrpcTagsAreCopiedToRootSpan()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

        // Act
        listener.OnStartActivity(frameworkActivity, context);
        frameworkActivity.SetTag(GrpcTagHelper.GrpcMethodTagName, "/package.Service/Method");
        listener.OnStopActivity(Activity.Current!, context);
        frameworkActivity.Stop();

        // Assert
        var activity = Assert.Single(exportedItems);

        Assert.Null(activity.ParentId);
        Assert.Equal("/package.Service/Method", activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
    }

    [Fact]
    public void FrameworkActivityIsKeptWhenRootSpanIsNotCreated()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tracerProvider = CreateTracerProvider(exportedItems);
        using var source = new ActivitySource(ActivitySourceName);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = CreateContext();
        context.Request.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";

        var listener = CreateListener();
        using var frameworkActivity = StartFrameworkActivity(source, new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true));

        Activity? current;

        // Act
        using (SuppressInstrumentationScope.Begin())
        {
            listener.OnStartActivity(frameworkActivity, context);
            current = Activity.Current;
        }

        // Assert
        Assert.Same(frameworkActivity, current);
    }

#if NET9_0_OR_GREATER
    [Fact]
    public async Task SignalRActivitiesAreListenedTo()
    {
        var exportedItems = new List<Activity>();
        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using (var server = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            await using var client = new HubConnectionBuilder()
                .WithUrl(server.Server.BaseAddress + "testHub", o =>
                {
                    o.HttpMessageHandlerFactory = _ => server.Server.CreateHandler();
                    o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                }).Build();
            await client.StartAsync(TestContext.Current.CancellationToken);

            await client.SendAsync("Send", "text", TestContext.Current.CancellationToken);

            await client.StopAsync(TestContext.Current.CancellationToken);

            // OnDisconnectedAsync runs on the server after the client has stopped, so give it
            // time to be exported before the server is disposed and can no longer export it.
            WaitForActivityExportToStabilize(exportedItems);
        }

        var hubActivity = exportedItems
            .Where(a => a.DisplayName.StartsWith("TestApp.AspNetCore.TestHub", StringComparison.InvariantCulture));

        Assert.Equal(3, hubActivity.Count());
        Assert.Collection(
            hubActivity,
            one =>
            {
                Assert.Equal("TestApp.AspNetCore.TestHub/OnConnectedAsync", one.DisplayName);
            },
            two =>
            {
                Assert.Equal("TestApp.AspNetCore.TestHub/Send", two.DisplayName);
            },
            three =>
            {
                Assert.Equal("TestApp.AspNetCore.TestHub/OnDisconnectedAsync", three.DisplayName);
            });
    }

    [Fact]
    public async Task SignalRActivitiesCanBeDisabled()
    {
        var exportedItems = new List<Activity>();
        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation(o => o.EnableAspNetCoreSignalRSupport = false)
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        // Arrange
        using (var server = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            await using var client = new HubConnectionBuilder()
                .WithUrl(server.Server.BaseAddress + "testHub", o =>
                {
                    o.HttpMessageHandlerFactory = _ => server.Server.CreateHandler();
                    o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                }).Build();
            await client.StartAsync(TestContext.Current.CancellationToken);

            await client.SendAsync("Send", "text", TestContext.Current.CancellationToken);

            await client.StopAsync(TestContext.Current.CancellationToken);
        }

        WaitForActivityExportToStabilize(exportedItems);

        var hubActivity = exportedItems
            .Where(a => a.DisplayName.StartsWith("TestApp.AspNetCore.TestHub", StringComparison.InvariantCulture));

        Assert.Empty(hubActivity);
    }

    [Fact]
    public async Task RazorComponentsActivitiesCanBeDisabled()
    {
        var exportedItems = new List<Activity>();
        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation(o => o.EnableRazorComponentsSupport = false)
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            var fakeActivitySource = new ActivitySource("Microsoft.AspNetCore.Components");
            var activity = fakeActivitySource.CreateActivity("Microsoft.AspNetCore.Components.HandleEvent", ActivityKind.Internal, parentId: null, null, null);
            if (activity != null)
            {
                activity.Start();
                activity.SetTag("aspnetcore.components.type", "BasicTests");
                activity.SetTag("aspnetcore.components.method", "BlazorActivitiesCanBeDisabled");
                activity.Stop();
            }

            try
            {
                using var response = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);
            }
            catch (Exception)
            {
                // ignore errors
            }

            WaitForActivityExport(exportedItems, 1);
        }

        var blazorActivity = exportedItems
            .Where(a => a.DisplayName.StartsWith("Microsoft.AspNetCore.Components", StringComparison.InvariantCulture));

        Assert.Empty(blazorActivity);
    }

#if NET10_0_OR_GREATER
    [Fact]
    public async Task RazorComponentsActivitiesAreEnabledByDefault()
    {
        var exportedItems = new List<Activity>();
        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            var fakeActivitySource = new ActivitySource("Microsoft.AspNetCore.Components");
            var activity = fakeActivitySource.CreateActivity("Microsoft.AspNetCore.Components.HandleEvent", ActivityKind.Internal, parentId: null, null, null);
            if (activity != null)
            {
                activity.Start();
                activity.SetTag("aspnetcore.components.type", "BasicTests");
                activity.SetTag("aspnetcore.components.method", "BlazorActivitiesCanBeDisabled");
                activity.Stop();
            }

            try
            {
                using var response = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);
            }
            catch (Exception)
            {
                // ignore errors
            }

            WaitForActivityExport(exportedItems, 1);
        }

        var blazorActivity = exportedItems
            .Where(a => a.DisplayName.StartsWith("Microsoft.AspNetCore.Components", StringComparison.InvariantCulture));

        Assert.NotEmpty(blazorActivity);
    }
#endif

#endif

    [Fact]
    public async Task EnrichCallbackNotCalledMultipleTimesWhenInstrumentationAddedTwice()
    {
        // When AddAspNetCoreInstrumentation is called multiple times (e.g., by a distro package
        // and the user), enrich callbacks should only fire once per request, not once per registration.
        var callCountDefault = 0;
        var callCountNamed = 0;

        var exportedItems = new List<Activity>();

        void ConfigureTestServices(IServiceCollection services)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddAspNetCoreInstrumentation(options => options.EnrichWithHttpRequest = (activity, request) => callCountDefault++)
                .AddAspNetCoreInstrumentation("named", options => options.EnrichWithHttpRequest = (activity, request) => callCountNamed++)
                .AddAspNetCoreInstrumentation("named", options => options.EnrichWithHttpRequest = (activity, request) => callCountNamed++)
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        using (var client = this.CreateClient(builder =>
            {
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            }))
        {
            using var response = await client.GetAsync(new Uri("/api/values", UriKind.Relative), TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Equal(1, callCountDefault);
        Assert.Equal(1, callCountNamed);
        Assert.Single(exportedItems);
    }

    public void Dispose()
    {
        this.tracerProvider?.Dispose();

        foreach (var derivedFactory in this.derivedFactories)
        {
            derivedFactory.Dispose();
        }

        this.derivedFactories.Clear();
    }

    private static void WaitForActivityExport(List<Activity> exportedItems, int count)
        => Assert.True(
            SpinWait.SpinUntil(
            () =>
            {
                // We need to let End callback execute as it is executed AFTER response was returned.
                // In unit tests environment there may be a lot of parallel unit tests executed, so
                // giving some breathing room for the End callback to complete
                Thread.Sleep(10);
                return exportedItems.Count >= count;
            },
            TimeSpan.FromSeconds(5)),
            $"Actual: {exportedItems.Count} Expected: {count}");

    private static void WaitForEventCount(Func<int> getCount, int count)
        => Assert.True(
            SpinWait.SpinUntil(
            () =>
            {
                // The OnStop (End) callback is executed AFTER the response has been
                // returned to the client, so the count may not have reached its final
                // value when the request completes. Give the callback time to run.
                Thread.Sleep(10);
                return getCount() >= count;
            },
            TimeSpan.FromSeconds(5)),
            $"Actual: {getCount()} Expected: {count}");

#if NET9_0_OR_GREATER
    private static void WaitForActivityExportToStabilize(List<Activity> exportedItems)
    {
        // The number of activities produced by the SignalR long-polling transport is
        // non-deterministic, so instead of waiting for an exact count (which is flaky)
        // we wait until no new activities have been exported for a short, quiet period.
        var lastCount = -1;
        var stableChecks = 0;

        SpinWait.SpinUntil(
            () =>
            {
                Thread.Sleep(50);
                var currentCount = exportedItems.Count;
                if (currentCount != lastCount)
                {
                    lastCount = currentCount;
                    stableChecks = 0;
                    return false;
                }

                return ++stableChecks >= 10;
            },
            TimeSpan.FromSeconds(5));
    }
#endif

    private static void ValidateAspNetCoreActivity(Activity activityToValidate, string expectedHttpPath)
    {
        Assert.Equal(ActivityKind.Server, activityToValidate.Kind);
        Assert.Equal(HttpInListener.AspNetCoreActivitySourceName, activityToValidate.Source.Name);
        Assert.NotNull(activityToValidate.Source.Version);
        Assert.Empty(activityToValidate.Source.Version);
        Assert.Equal(expectedHttpPath, activityToValidate.GetTagValue(SemanticConventions.AttributeUrlPath) as string);
    }

    private static void AssertException(List<Activity> exportedItems)
    {
        var activity = Assert.Single(exportedItems);

        var exMessage = "something's wrong!";
        var item = Assert.Single(activity.Events);
        Assert.Equal("System.Exception", item.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
        Assert.Equal(exMessage, item.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);

        ValidateAspNetCoreActivity(activity, "/api/error");
    }

    private static TracerProvider CreateTracerProvider(List<Activity> exportedItems) =>
        Sdk.CreateTracerProviderBuilder()
            .AddSource(ActivitySourceName)
            .AddInMemoryExporter(exportedItems)
            .Build();

    private static HttpInListener CreateListener(Action<AspNetCoreTraceInstrumentationOptions>? configure = null)
    {
        var options = new AspNetCoreTraceInstrumentationOptions
        {
            EnableNewRootSpan = true,
        };

        configure?.Invoke(options);

        return new(options);
    }

    private static Activity StartFrameworkActivity(ActivitySource source, ActivityContext parentContext = default)
    {
        var activity = source.CreateActivity(HttpInListener.ActivityOperationName, ActivityKind.Server, parentContext)!;
        activity.Start();
        return activity;
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();

        context.Request.Method = "GET";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = "/api/values";

        return context;
    }

    private HttpClient CreateClient(Action<IWebHostBuilder> configureWebHost)
    {
        var derivedFactory = this.factory.WithWebHostBuilder(configureWebHost);
        this.derivedFactories.Add(derivedFactory);
        return derivedFactory.CreateClient();
    }

    private void ConfigureExceptionFilters(IServiceCollection services, int mode, ref List<Activity> exportedItems)
    {
        switch (mode)
        {
            case 1:
                services.AddMvc(x => x.Filters.Add<ExceptionFilter1>());
                break;
            case 2:
                services.AddMvc(x => x.Filters.Add<ExceptionFilter1>());
                services.AddMvc(x => x.Filters.Add<ExceptionFilter2>());
                break;
            default:
                break;
        }

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation(x => x.RecordException = true)
            .AddInMemoryExporter(exportedItems)
            .Build();
    }

    private class ExtractOnlyPropagator(ActivityContext activityContext, Baggage baggage) : TextMapPropagator
    {
        private readonly ActivityContext activityContext = activityContext;
        private readonly Baggage baggage = baggage;

        public override ISet<string> Fields => throw new NotImplementedException();

        public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
            => new(this.activityContext, this.baggage);

        public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
            => throw new NotImplementedException();
    }

    private class TestSampler(SamplingDecision samplingDecision, IEnumerable<KeyValuePair<string, object>>? attributes = null) : Sampler
    {
        private readonly SamplingDecision samplingDecision = samplingDecision;
        private readonly IEnumerable<KeyValuePair<string, object>>? attributes = attributes;

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => new(this.samplingDecision, this.attributes);
    }

    private class TestHttpInListener(AspNetCoreTraceInstrumentationOptions options) : HttpInListener(options)
    {
        public Action<string, object?>? OnEventWrittenCallback;

        public override void OnEventWritten(string name, object? payload)
        {
            base.OnEventWritten(name, payload);

            this.OnEventWrittenCallback?.Invoke(name, payload);
        }
    }

    private class TestNullHostActivityMiddlewareImpl(string activitySourceName, string activityName) : TestActivityMiddleware
    {
        private readonly ActivitySource activitySource = new(activitySourceName);
        private readonly string activityName = activityName;
        private Activity? activity;

        public override void PreProcess(HttpContext context)
        {
            // Setting the host activity i.e. activity started by asp.net core
            // to null here will have no impact on middleware activity.
            // This also means that asp.net core activity will not be found
            // during OnEventWritten event.
            Activity.Current = null;
            this.activity = this.activitySource.StartActivity(this.activityName);
        }

        public override void PostProcess(HttpContext context)
            => this.activity?.Stop();
    }

    private class TestTestActivityMiddleware(string activitySourceName, string activityName) : TestActivityMiddleware
    {
        private readonly ActivitySource activitySource = new(activitySourceName);
        private readonly string activityName = activityName;
        private Activity? activity;

        public override void PreProcess(HttpContext context)
            => this.activity = this.activitySource.StartActivity(this.activityName);

        public override void PostProcess(HttpContext context)
            => this.activity?.Stop();
    }
}
