// <copyright file="AspNetCoreEnrichmentExtensionsTests.cs" company="OpenTelemetry Authors">
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
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore.Tests;

public sealed class AspNetCoreEnrichmentExtensionsTests
{
    [Fact]
    public async Task TraceProviderBuilder_AddAspNetCoreTraceEnricherT_RegistersEnricher()
    {
        var exportedItems = new List<Activity>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher>()
                    .AddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher2>()
                    .AddAspNetCoreInstrumentation()
                    .AddInMemoryExporter(exportedItems)
                    .Build();
                services.AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet("/api/values", context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);
        await server.Host.StartAsync();

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        Assert.NotEmpty(tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.RequestKey && (int)tag.Value == 1));
        Assert.NotEmpty(tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.ResponseKey && (int)tag.Value == 1));

        Assert.NotEmpty(tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.RequestKey && (int)tag.Value == 1));
        Assert.NotEmpty(tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.ResponseKey && (int)tag.Value == 1));

        await server.Host.StopAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task TraceProviderBuilder_AddAspNetCoreTraceEnricher_RegistersEnricher()
    {
        var enricher1 = new Mock<AspNetCoreTraceEnricher>();
        var enricher2 = new Mock<AspNetCoreTraceEnricher>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreTraceEnricher(enricher1.Object)
                    .AddAspNetCoreTraceEnricher(enricher2.Object)
                    .AddAspNetCoreInstrumentation()
                    .Build();
                services.AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet("/api/values", context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);
        await server.Host.StartAsync();

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        enricher1.Verify(e => e.EnrichWithHttpRequest(It.IsAny<TraceEnrichmentBag>(), It.IsAny<HttpRequest>()), Times.Once);
        enricher1.Verify(e => e.EnrichWithHttpResponse(It.IsAny<TraceEnrichmentBag>(), It.IsAny<HttpResponse>()), Times.Once);
        enricher2.Verify(e => e.EnrichWithHttpRequest(It.IsAny<TraceEnrichmentBag>(), It.IsAny<HttpRequest>()), Times.Once);
        enricher2.Verify(e => e.EnrichWithHttpResponse(It.IsAny<TraceEnrichmentBag>(), It.IsAny<HttpResponse>()), Times.Once);

        await server.Host.StopAsync().ConfigureAwait(false);
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
                    .AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddAspNetCoreInstrumentation()
                        .AddInMemoryExporter(exportedItems))
                        .Services
                    .AddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher>()
                    .AddAspNetCoreTraceEnricher<MyAspNetCoreTraceEnricher2>()
                    .AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet("/api/values", context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);
        await server.Host.StartAsync();

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            WaitForActivityExport(exportedItems, 1);
        }

        Assert.Single(exportedItems);
        var tagObjects = exportedItems[0].TagObjects;

        Assert.NotEmpty(tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.RequestKey && (int)tag.Value == 1));
        Assert.NotEmpty(tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher.ResponseKey && (int)tag.Value == 1));

        Assert.NotEmpty(tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.RequestKey && (int)tag.Value == 1));
        Assert.NotEmpty(tagObjects.Where(tag => tag.Key == MyAspNetCoreTraceEnricher2.ResponseKey && (int)tag.Value == 1));

        await server.Host.StopAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task IServiceCollection_AddAspNetCoreTraceEnricher_RegistersEnricher()
    {
        var enricher1 = new Mock<AspNetCoreTraceEnricher>();
        var enricher2 = new Mock<AspNetCoreTraceEnricher>();

        var webHostBuilder = new WebHostBuilder()
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services
                    .AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddAspNetCoreInstrumentation())
                        .Services
                    .AddAspNetCoreTraceEnricher(enricher1.Object)
                    .AddAspNetCoreTraceEnricher(enricher2.Object)
                    .AddRouting();
            })
            .Configure(app => app
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapGet("/api/values", context => context.Response.WriteAsync("GetCompleted"))));

        using var server = new TestServer(webHostBuilder);
        await server.Host.StartAsync();

        using var client = server.CreateClient();
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        enricher1.Verify(e => e.EnrichWithHttpRequest(It.IsAny<TraceEnrichmentBag>(), It.IsAny<HttpRequest>()), Times.Once);
        enricher1.Verify(e => e.EnrichWithHttpResponse(It.IsAny<TraceEnrichmentBag>(), It.IsAny<HttpResponse>()), Times.Once);
        enricher2.Verify(e => e.EnrichWithHttpRequest(It.IsAny<TraceEnrichmentBag>(), It.IsAny<HttpRequest>()), Times.Once);
        enricher2.Verify(e => e.EnrichWithHttpResponse(It.IsAny<TraceEnrichmentBag>(), It.IsAny<HttpResponse>()), Times.Once);

        await server.Host.StopAsync().ConfigureAwait(false);
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
