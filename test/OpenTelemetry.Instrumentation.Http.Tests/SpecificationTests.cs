// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http.Tests;

[Trait("Category", "Weaver")]
public sealed class SpecificationTests : WeaverSpecificationTests
{
    private readonly ITestOutputHelper outputHelper;
    private readonly IDisposable serverLifeTime;
    private readonly string url;

    public SpecificationTests(WeaverFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
        this.outputHelper = outputHelper;
        this.serverLifeTime = TestHttpServer.RunServer(
            (context) =>
            {
                Thread.Sleep(50);
                context.Response.StatusCode = 200;
                context.Response.OutputStream.Close();
            },
            out var baseAddress);

        this.url = baseAddress.ToString();
        this.outputHelper.WriteLine($"HttpServer started: {this.url}");
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task Telemetry_Conforms_To_Semantic_Conventions()
    {
        // Arrange
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                                       .AddHttpClientInstrumentation()
                                       .AddInMemoryExporter(activities)
                                       .Build())
        {
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                                         .AddHttpClientInstrumentation()
                                         .AddInMemoryExporter(metrics)
                                         .Build();

            using (var httpClient = new HttpClient())
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, this.url);
                _ = await httpClient.SendAsync(request);
            }

            meterProvider.ForceFlush();
        }

        // Act and Assert
        await this.AssertTelemetryConformsToSemanticConventions(
            (activities, metrics),
            HttpClientInstrumentation.SemanticConventionsVersion,
            [new("missing_metric", "http.client.request.time_in_queue")]); // See https://github.com/open-telemetry/semantic-conventions/issues/3720
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.serverLifeTime?.Dispose();
        }

        base.Dispose(disposing);
    }
}
