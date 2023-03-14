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
    [Fact]
    public async Task TracerProviderBuilder_AddAspNetCoreTraceEnricherT_RegistersEnricher()
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
                .UseEndpoints(endpoints => endpoints.MapGet("/api/values", context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        var tagObject1 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.RequestKey);
        Assert.Equal(1, tagObject1.Single().Value);

        var tagObject2 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.ResponseKey);
        Assert.Equal(1, tagObject2.Single().Value);

        var tagObject3 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.RequestKey);
        Assert.Equal(1, tagObject3.Single().Value);

        var tagObject4 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.ResponseKey);
        Assert.Equal(1, tagObject4.Single().Value);
    }

    [Fact]
    public async Task TracerProviderBuilder_AddAspNetCoreTraceEnricher_RegistersEnricher()
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
                .UseEndpoints(endpoints => endpoints.MapGet("/api/values", context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        var tagObject1 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.RequestKey);
        Assert.Equal(1, tagObject1.Single().Value);

        var tagObject2 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.ResponseKey);
        Assert.Equal(1, tagObject2.Single().Value);

        var tagObject3 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.RequestKey);
        Assert.Equal(1, tagObject3.Single().Value);

        var tagObject4 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.ResponseKey);
        Assert.Equal(1, tagObject4.Single().Value);
    }

    [Fact]
    public async Task IServiceCollection_AddAspNetCoreTraceEnricherT_RegistersEnricher()
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
                .UseEndpoints(endpoints => endpoints.MapGet("/api/values", context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        var tagObject1 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.RequestKey);
        Assert.Equal(1, tagObject1.Single().Value);

        var tagObject2 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.ResponseKey);
        Assert.Equal(1, tagObject2.Single().Value);

        var tagObject3 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.RequestKey);
        Assert.Equal(1, tagObject3.Single().Value);

        var tagObject4 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.ResponseKey);
        Assert.Equal(1, tagObject4.Single().Value);
    }

    [Fact]
    public async Task IServiceCollection_AddAspNetCoreTraceEnricher_RegistersEnricher()
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
                .UseEndpoints(endpoints => endpoints.MapGet("/api/values", context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        var tagObject1 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.RequestKey);
        Assert.Equal(1, tagObject1.Single().Value);

        var tagObject2 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.ResponseKey);
        Assert.Equal(1, tagObject2.Single().Value);

        var tagObject3 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.RequestKey);
        Assert.Equal(1, tagObject3.Single().Value);

        var tagObject4 = tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.ResponseKey);
        Assert.Equal(1, tagObject4.Single().Value);
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
}
