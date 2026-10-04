// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

[Collection("AspNetCore")]
public class NewRootSpanTests
{
    private static readonly string ActivitySourceName = typeof(NewRootSpanTests).FullName!;

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
        var sampler = new TestSampler();
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
}
