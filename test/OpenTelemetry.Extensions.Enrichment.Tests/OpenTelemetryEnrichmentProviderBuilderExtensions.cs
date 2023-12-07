// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.Tests;

public sealed class OpenTelemetryEnrichmentProviderBuilderExtensions
{
    private const string SourceName = nameof(OpenTelemetryEnrichmentProviderBuilderExtensions);

    [Fact]
    public void AddTraceEnricherOfTRegistersEnricher()
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
            Assert.NotNull(activity);
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
    public void AddTraceEnricherRegistersEnricher()
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
            Assert.NotNull(activity);
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
    public void AddTraceEnricherActionRegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        const string testKey1 = "key1";
        const string testValue1 = "value1";
        const string testKey2 = "key2";
        const string testValue2 = "value2";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddTraceEnricher(bag => bag.Add(testKey1, testValue1))
            .AddTraceEnricher(bag => bag.Add(testKey2, testValue2))
            .AddInMemoryExporter(exportedItems)
            .Build();

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
    public void AddTraceEnricherFactoryRegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(SourceName)
            .AddTraceEnricher(sp => new MyTraceEnricher())
            .AddTraceEnricher(sp => new MyTraceEnricher2())
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var source1 = new ActivitySource(SourceName);

        using (var activity = source1.StartActivity(SourceName))
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
    }
}
