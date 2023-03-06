// <copyright file="EnrichmentExtensionsTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.Tests;

public sealed class EnrichmentExtensionsTests
{
    private const string SourceName = nameof(EnrichmentExtensionsTests);

    [Fact]
    public void TracerProviderBuilder_AddTraceEnricherT_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddTraceEnricher<MyTraceEnricher>()
            .AddTraceEnricher<MyTraceEnricher2>()
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var source = new ActivitySource(SourceName);
        using (var activity = source.StartActivity(SourceName))
        {
            activity.Stop();

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == MyTraceEnricher.Key);
            Assert.Equal(1, tagObject1.Single().Value);

            var tagObject2 = tagObjects.Where(tag => tag.Key == MyTraceEnricher2.Key);
            Assert.Equal(1, tagObject2.Single().Value);
        }
    }

    [Fact]
    public void TracerProviderBuilder_AddTraceEnricher_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddTraceEnricher(new MyTraceEnricher())
            .AddTraceEnricher(new MyTraceEnricher2())
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var source1 = new ActivitySource(SourceName);

        using (var activity = source1.StartActivity(SourceName))
        {
            activity.Stop();

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == MyTraceEnricher.Key);
            Assert.Equal(1, tagObject1.Single().Value);

            var tagObject2 = tagObjects.Where(tag => tag.Key == MyTraceEnricher2.Key);
            Assert.Equal(1, tagObject2.Single().Value);
        }
    }

    [Fact]
    public void TracerProviderBuilder_AddTraceEnricherAction_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        const string testKey = "key";
        const string testValue = "value";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddTraceEnricher(bag => bag.Add(testKey, testValue))
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var source1 = new ActivitySource(SourceName);

        using (var activity = source1.StartActivity(SourceName))
        {
            activity.Stop();

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == testKey);
            Assert.Equal(testValue, (string)tagObject1.Single().Value);
        }
    }

    [Fact]
    public async Task IServiceCollection_AddTraceEnricherT_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .AddTraceEnricher<MyTraceEnricher>()
                .AddTraceEnricher<MyTraceEnricher2>())
            .Build();

        await host.StartAsync().ConfigureAwait(false);

        var enrichers = host.Services.GetServices<TraceEnricher>().ToArray();
        Assert.NotNull(enrichers);
        Assert.Equal(2, enrichers.Length);

        using var source = new ActivitySource(SourceName);
        using (var activity = source.StartActivity(SourceName))
        {
            activity.Stop();

            Assert.Equal(1, (enrichers[0] as MyTraceEnricher).TimesCalled);
            Assert.Equal(1, (enrichers[1] as MyTraceEnricher2).TimesCalled);

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == MyTraceEnricher.Key);
            Assert.Equal(1, tagObject1.Single().Value);

            var tagObject2 = tagObjects.Where(tag => tag.Key == MyTraceEnricher2.Key);
            Assert.Equal(1, tagObject2.Single().Value);
        }

        await host.StopAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task IServiceCollection_AddTraceEnricher_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName)
                    .AddInMemoryExporter(exportedItems))
                .Services
                .AddTraceEnricher(new MyTraceEnricher())
                .AddTraceEnricher(new MyTraceEnricher2()))
            .Build();

        await host.StartAsync().ConfigureAwait(false);

        var enrichers = host.Services.GetServices<TraceEnricher>().ToArray();
        Assert.NotNull(enrichers);
        Assert.Equal(2, enrichers.Length);

        using var source = new ActivitySource(SourceName);
        using (var activity = source.StartActivity(SourceName))
        {
            activity.Stop();

            Assert.Single(exportedItems);

            var tagObjects = exportedItems[0].TagObjects;
            var tagObject1 = tagObjects.Where(tag => tag.Key == MyTraceEnricher.Key);
            Assert.Equal(1, tagObject1.Single().Value);

            var tagObject2 = tagObjects.Where(tag => tag.Key == MyTraceEnricher2.Key);
            Assert.Equal(1, tagObject2.Single().Value);
        }

        await host.StopAsync().ConfigureAwait(false);
    }
}
