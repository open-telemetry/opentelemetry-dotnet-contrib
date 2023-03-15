// <copyright file="OpenTelemetryAspNetCoreEnrichmentExtensionsTests.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore.Tests;

public sealed class OpenTelemetryAspNetCoreEnrichmentExtensionsTests
{
    private const string UriPattern = "/api/values";
    private readonly Uri uri = new Uri(UriPattern, UriKind.Relative);

    [Fact]
    public async Task TracerProviderBuilderAddAspNetCoreTraceEnricherTEnrichesActivity()
    {
        var exportedItems = new List<Activity>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher>()
                        .AddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher2>()
                        .AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(exportedItems));
                services.AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet(UriPattern, context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync(this.uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.ResponseKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.ResponseKey, 1);
    }

    [Fact]
    public async Task TracerProviderBuilderAddAspNetCoreTraceEnricherEnrichesActivity()
    {
        var exportedItems = new List<Activity>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddAspNetCoreTraceEnricher(new MyAspNetCoreTraceEnricher())
                        .AddAspNetCoreTraceEnricher(new MyAspNetCoreTraceEnricher2())
                        .AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(exportedItems));
                services.AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet(UriPattern, context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync(this.uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.ResponseKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.ResponseKey, 1);
    }

    [Fact]
    public async Task TracerProviderBuilderAddAspNetCoreTraceEnricherFactoryEnrichesActivity()
    {
        var exportedItems = new List<Activity>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddAspNetCoreTraceEnricher(sp => new MyAspNetCoreTraceEnricher())
                        .AddAspNetCoreTraceEnricher(sp => new MyAspNetCoreTraceEnricher2())
                        .AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(exportedItems));
                services.AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet(UriPattern, context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync(this.uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.ResponseKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.ResponseKey, 1);
    }

    [Fact]
    public async Task IServiceCollectionAddAspNetCoreTraceEnricherTEnrichesActivity()
    {
        var exportedItems = new List<Activity>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services
                    .AddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher>()
                    .AddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher2>()
                    .AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(exportedItems))
                        .Services
                    .AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet(UriPattern, context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync(this.uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.ResponseKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.ResponseKey, 1);
    }

    [Fact]
    public async Task IServiceCollectionAddAspNetCoreTraceEnricherEnrichesActivity()
    {
        var exportedItems = new List<Activity>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services
                    .AddAspNetCoreTraceEnricher(new MyAspNetCoreTraceEnricher())
                    .AddAspNetCoreTraceEnricher(new MyAspNetCoreTraceEnricher2())
                    .AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(exportedItems))
                        .Services
                    .AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet(UriPattern, context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync(this.uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.ResponseKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.ResponseKey, 1);
    }

    [Fact]
    public async Task IServiceCollectionAddAspNetCoreTraceEnricherFactoryEnrichesActivity()
    {
        var exportedItems = new List<Activity>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services
                    .AddAspNetCoreTraceEnricher(sp => new MyAspNetCoreTraceEnricher())
                    .AddAspNetCoreTraceEnricher(sp => new MyAspNetCoreTraceEnricher2())
                    .AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(exportedItems))
                        .Services
                    .AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet(UriPattern, context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync(this.uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher.ResponseKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.RequestKey, 1);
        AssertTagHasValue(tagObjects, MyAspNetCoreTraceEnricher2.ResponseKey, 1);
    }

    private static void WaitForActivityExport(List<Activity> exportedItems, int count)
    {
        // We need to let End callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the End callback to complete
        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                Thread.Sleep(10);
                return exportedItems.Count >= count;
            },
            TimeSpan.FromSeconds(1)));
    }

    private static void AssertTagHasValue(IEnumerable<KeyValuePair<string, object>> tagObjects, string key, object value)
    {
        var tagObject = tagObjects.Where(tag => tag.Key == key);
        Assert.Equal(value, tagObject.Single().Value);
    }
}
