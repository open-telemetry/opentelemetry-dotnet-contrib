// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.Tests;

public sealed class OpenTelemetryEnrichmentServiceCollectionExtensionsTests
{
    private const string SourceName = nameof(OpenTelemetryEnrichmentServiceCollectionExtensionsTests);

    [Fact]
    public async Task GenericMethod_TRegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .TryAddTraceEnricher<MyTraceEnricher>()
                .TryAddTraceEnricher<MyTraceEnricher2>())
            .Build();

        await host.StartAsync();

        var enrichers = host.Services.GetServices<TraceEnricher>().ToArray();
        Assert.NotNull(enrichers);
        Assert.Equal(2, enrichers.Length);

        using var source = new ActivitySource(SourceName);
        using (var activity = source.StartActivity(SourceName))
        {
            Assert.NotNull(activity);
            activity.Stop();

            var myTraceEnricher = enrichers[0] as MyTraceEnricher;
            var myTraceEnricher2 = enrichers[1] as MyTraceEnricher2;
            Assert.NotNull(myTraceEnricher);
            Assert.NotNull(myTraceEnricher2);
            Assert.Equal(1, myTraceEnricher.TimesCalled);
            Assert.Equal(1, myTraceEnricher2.TimesCalled);

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == MyTraceEnricher.Key);
            Assert.Equal(1, tagObject1.Single().Value);

            var tagObject2 = tagObjects.Where(tag => tag.Key == MyTraceEnricher2.Key);
            Assert.Equal(1, tagObject2.Single().Value);
        }

        await host.StopAsync();
    }

    [Fact]
    public async Task InstanceMethod_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .TryAddTraceEnricher(new MyTraceEnricher())
                .TryAddTraceEnricher(new MyTraceEnricher2()))
            .Build();

        await host.StartAsync();

        var enrichers = host.Services.GetServices<TraceEnricher>().ToArray();
        Assert.NotNull(enrichers);
        Assert.Equal(2, enrichers.Length);

        using var source = new ActivitySource(SourceName);
        using (var activity = source.StartActivity(SourceName))
        {
            Assert.NotNull(activity);
            activity.Stop();

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == MyTraceEnricher.Key);
            Assert.Equal(1, tagObject1.Single().Value);

            var tagObject2 = tagObjects.Where(tag => tag.Key == MyTraceEnricher2.Key);
            Assert.Equal(1, tagObject2.Single().Value);
        }

        await host.StopAsync();
    }

    [Fact]
    public async Task DelegateMethod_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        const string testKey1 = "key1";
        const string testValue1 = "value1";
        const string testKey2 = "key2";
        const string testValue2 = "value2";

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .AddTraceEnricher(bag => bag.Add(testKey1, testValue1))
                .AddTraceEnricher(bag => bag.Add(testKey2, testValue2)))
            .Build();

        await host.StartAsync();

        using var source1 = new ActivitySource(SourceName);

        using (var activity = source1.StartActivity(SourceName))
        {
            Assert.NotNull(activity);
            activity.Stop();

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == testKey1);
            Assert.Equal(testValue1, tagObject1.Single().Value);

            var tagObject2 = tagObjects.Where(tag => tag.Key == testKey2);
            Assert.Equal(testValue2, tagObject2.Single().Value);
        }
    }

    [Fact]
    public async Task FactoryMethod_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .AddTraceEnricher(sp => new MyTraceEnricher())
                .AddTraceEnricher(sp => new MyTraceEnricher2()))
            .Build();

        await host.StartAsync();

        var enrichers = host.Services.GetServices<TraceEnricher>().ToArray();
        Assert.NotNull(enrichers);
        Assert.Equal(2, enrichers.Length);

        using var source = new ActivitySource(SourceName);
        using (var activity = source.StartActivity(SourceName))
        {
            Assert.NotNull(activity);
            activity.Stop();

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == MyTraceEnricher.Key);
            Assert.Equal(1, tagObject1.Single().Value);

            var tagObject2 = tagObjects.Where(tag => tag.Key == MyTraceEnricher2.Key);
            Assert.Equal(1, tagObject2.Single().Value);
        }

        await host.StopAsync();
    }

    [Fact]
    public async Task DelegateMethod_SwallowsExceptionsFromEnrichmentActions()
    {
        var exportedItems = new List<Activity>();

        const string testKey = "safe-key";
        const string testValue = "safe-value";

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .AddTraceEnricher((Action<TraceEnrichmentBag>)(_ => throw new InvalidOperationException("boom")))
                .AddTraceEnricher((Action<TraceEnrichmentBag>)(bag => bag.Add(testKey, testValue))))
            .Build();

        await host.StartAsync();

        using var source = new ActivitySource(SourceName);

        var exception = Record.Exception(() =>
        {
            using var activity = source.StartActivity(SourceName);
            Assert.NotNull(activity);
            activity.Stop();
        });

        Assert.Null(exception);
        Assert.Single(exportedItems);
        Assert.Equal(testValue, exportedItems[0].TagObjects.Single(tag => tag.Key == testKey).Value);

        await host.StopAsync();
    }

    [Fact]
    public async Task InstanceMethod_SwallowsExceptionsFromEnrich()
    {
        var exportedItems = new List<Activity>();
        var trackingEnricher = new TrackingTraceEnricher();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .TryAddTraceEnricher(new ThrowingOnEndTraceEnricher())
                .TryAddTraceEnricher(trackingEnricher))
            .Build();

        await host.StartAsync();

        using var source = new ActivitySource(SourceName);

        var exception = Record.Exception(() =>
        {
            using var activity = source.StartActivity(SourceName);
            Assert.NotNull(activity);
            activity.Stop();
        });

        Assert.Null(exception);
        Assert.Equal(1, trackingEnricher.StartCalls);
        Assert.Equal(1, trackingEnricher.EndCalls);
        Assert.Single(exportedItems);
        Assert.Equal(TrackingTraceEnricher.EndValue, exportedItems[0].TagObjects.Single(tag => tag.Key == TrackingTraceEnricher.EndKey).Value);

        await host.StopAsync();
    }

    [Fact]
    public async Task InstanceMethod_SwallowsExceptionsFromEnrichOnActivityStart()
    {
        var exportedItems = new List<Activity>();
        var trackingEnricher = new TrackingTraceEnricher();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .TryAddTraceEnricher(new ThrowingOnStartTraceEnricher())
                .TryAddTraceEnricher(trackingEnricher))
            .Build();

        await host.StartAsync();

        using var source = new ActivitySource(SourceName);

        var exception = Record.Exception(() =>
        {
            using var activity = source.StartActivity(SourceName);
            Assert.NotNull(activity);
            activity.Stop();
        });

        Assert.Null(exception);
        Assert.Equal(1, trackingEnricher.StartCalls);
        Assert.Equal(1, trackingEnricher.EndCalls);
        Assert.Single(exportedItems);
        Assert.Equal(TrackingTraceEnricher.StartValue, exportedItems[0].TagObjects.Single(tag => tag.Key == TrackingTraceEnricher.StartKey).Value);

        await host.StopAsync();
    }

    private sealed class ThrowingOnStartTraceEnricher : TraceEnricher
    {
        public override void Enrich(in TraceEnrichmentBag bag)
        {
        }

        public override void EnrichOnActivityStart(in TraceEnrichmentBag bag)
            => throw new InvalidOperationException("boom");
    }

    private sealed class ThrowingOnEndTraceEnricher : TraceEnricher
    {
        public override void Enrich(in TraceEnrichmentBag bag)
            => throw new InvalidOperationException("boom");
    }

    private sealed class TrackingTraceEnricher : TraceEnricher
    {
        public const string StartKey = "tracking-start";
        public const string StartValue = "start";
        public const string EndKey = "tracking-end";
        public const string EndValue = "end";

        public int StartCalls { get; private set; }

        public int EndCalls { get; private set; }

        public override void Enrich(in TraceEnrichmentBag bag)
        {
            this.EndCalls++;
            bag.Add(EndKey, EndValue);
        }

        public override void EnrichOnActivityStart(in TraceEnrichmentBag bag)
        {
            this.StartCalls++;
            bag.Add(StartKey, StartValue);
        }
    }
}
