// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.Http.Tests;

public class HttpClientTraceEnrichmentAcceptanceTests : IDisposable
{
    private readonly IDisposable serverLifeTime;
    private readonly string url;

    public HttpClientTraceEnrichmentAcceptanceTests()
    {
        this.serverLifeTime = TestHttpServer.RunServer(
            ctx =>
            {
                var responseCode = ctx.Request.Headers["responseCode"];
                ctx.Response.StatusCode = responseCode != null ? int.Parse(responseCode) : 200;
                ctx.Response.OutputStream.Close();
            },
            out var host,
            out var port);

        this.url = $"http://{host}:{port}/";
    }

    [Fact]
    public async Task GivenHttpRequest_EnrichedOnRequestAndResponse()
    {
        var exportedActivities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.TryAddHttpClientTraceEnricher<AcceptanceTestEnricher>();
            })
            .AddHttpClientInstrumentation()
            .AddInMemoryExporter(exportedActivities)
            .Build();

        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, this.url);

        // Act
        using var response = await httpClient.SendAsync(request);

        // Assert
        var activity = Assert.Single(exportedActivities);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("GET", activity.DisplayName);

        // Enrichment tags
        Assert.Equal("GET", activity.GetTagItem("accept.request.method"));
        Assert.Equal(200, activity.GetTagItem("accept.response.status"));
        Assert.Null(activity.GetTagItem("accept.exception"));
    }

    [Fact]
    public async Task GivenFailedRequest_EnrichesOnException()
    {
        var exportedActivities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.TryAddHttpClientTraceEnricher<AcceptanceTestEnricher>())
            .AddHttpClientInstrumentation(o => o.RecordException = true)
            .AddInMemoryExporter(exportedActivities)
            .Build();

        using var httpClient = new HttpClient();
        var invalidUrl = new Uri("http://nonexistent.invalid-domain-for-otel-tests-xyz/");

        Exception? thrown = null;
        try
        {
            await httpClient.GetAsync(invalidUrl);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Assert.NotNull(thrown); // network failure expected

        var activity = Assert.Single(exportedActivities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.NotNull(activity.GetTagItem("accept.exception"));
    }

    public void Dispose()
    {
        this.serverLifeTime.Dispose();
        Activity.Current = null;
        GC.SuppressFinalize(this);
    }
}
