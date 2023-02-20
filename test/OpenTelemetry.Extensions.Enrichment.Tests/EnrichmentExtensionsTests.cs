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

using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.Tests;

public sealed class EnrichmentExtensionsTests
{
    private const string SourceName = nameof(EnrichmentExtensionsTests);

    [Fact]
    public void TracerProviderBuilder_AddTraceEnricherT_RegistersEnricher()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddTraceEnricher<MyTraceEnricher>()
            .AddTraceEnricher<MyTraceEnricher2>()
            .Build();

        using var source = new ActivitySource(SourceName);
        using (var activity = source.StartActivity(SourceName))
        {
            activity.Stop();
            Assert.Equal(1, (int)activity.GetTagItem(MyTraceEnricher.Key));
            Assert.Equal(1, (int)activity.GetTagItem(MyTraceEnricher2.Key));
        }
    }

    [Fact]
    public void TracerProviderBuilder_AddTraceEnricher_RegistersEnricher()
    {
        var enricher1 = new Mock<TraceEnricher>();
        var enricher2 = new Mock<TraceEnricher>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddTraceEnricher(enricher1.Object)
            .AddTraceEnricher(enricher2.Object)
            .Build();

        using var source1 = new ActivitySource(SourceName);

        using (var activity = source1.StartActivity(SourceName))
        {
            activity.Stop();
            enricher1.Verify(e => e.Enrich(It.IsAny<TraceEnrichmentBag>()), Times.Once);
            enricher2.Verify(e => e.Enrich(It.IsAny<TraceEnrichmentBag>()), Times.Once);
        }
    }

    [Fact]
    public void TracerProviderBuilder_AddTraceEnricherAction_RegistersEnricher()
    {
        const string testKey = "key";
        const string testValue = "value";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddTraceEnricher(bag => bag.Add(testKey, testValue))
            .Build();

        using var source1 = new ActivitySource(SourceName);

        using (var activity = source1.StartActivity(SourceName))
        {
            activity.Stop();
            Assert.Equal(testValue, (string)activity.GetTagItem(testKey));
        }
    }

    [Fact]
    public async Task IServiceCollection_AddTraceEnricherT_RegistersEnricher()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName))
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
            Assert.Equal(1, (int)activity.GetTagItem(MyTraceEnricher.Key));
            Assert.Equal(1, (int)activity.GetTagItem(MyTraceEnricher2.Key));
        }

        await host.StopAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task IServiceCollection_AddTraceEnricher_RegistersEnricher()
    {
        var enricher1 = new Mock<TraceEnricher>();
        var enricher2 = new Mock<TraceEnricher>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services
                .AddOpenTelemetry()
                .WithTracing(builder => builder
                    .AddSource(SourceName))
                .Services
                .AddTraceEnricher(enricher1.Object)
                .AddTraceEnricher(enricher2.Object))
            .Build();

        await host.StartAsync().ConfigureAwait(false);

        var enrichers = host.Services.GetServices<TraceEnricher>().ToArray();
        Assert.NotNull(enrichers);
        Assert.Equal(2, enrichers.Length);

        using var source = new ActivitySource(SourceName);
        using (var activity = source.StartActivity(SourceName))
        {
            activity.Stop();
            enricher1.Verify(e => e.Enrich(It.IsAny<TraceEnrichmentBag>()), Times.Once);
            enricher2.Verify(e => e.Enrich(It.IsAny<TraceEnrichmentBag>()), Times.Once);
        }

        await host.StopAsync().ConfigureAwait(false);
    }
}
