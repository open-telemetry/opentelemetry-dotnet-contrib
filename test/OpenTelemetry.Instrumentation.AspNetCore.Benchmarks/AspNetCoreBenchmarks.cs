// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Benchmarks;

[MemoryDiagnoser]
public class AspNetCoreBenchmarks
{
    private static readonly Uri BaseAddress = new("/", UriKind.Relative);

    private HttpClient? httpClient;
    private WebApplication? app;
    private TracerProvider? tracerProvider;
    private MeterProvider? meterProvider;

    [Flags]
    public enum EnableInstrumentationOption
    {
        /// <summary>
        /// Instrumentation is not enabled for any signal.
        /// </summary>
        None = 0,

        /// <summary>
        /// Instrumentation is enabled only for Traces.
        /// </summary>
        Traces = 1,

        /// <summary>
        /// Instrumentation is enabled only for Metrics.
        /// </summary>
        Metrics = 2,
    }

    [Params(EnableInstrumentationOption.None, EnableInstrumentationOption.Traces, EnableInstrumentationOption.Metrics, EnableInstrumentationOption.Traces | EnableInstrumentationOption.Metrics)]
    public EnableInstrumentationOption EnableInstrumentation { get; set; }

    [GlobalSetup]
    public async Task StartServer()
    {
        await this.StartWebApplicationAsync();

        if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Traces))
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .Build();
        }

        if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Metrics))
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .Build();
        }
    }

    [GlobalCleanup]
    public async Task StopServer()
    {
        this.httpClient?.Dispose();

        if (this.app != null)
        {
            await this.app.StopAsync();
            await this.app.DisposeAsync();
        }

        this.tracerProvider?.Dispose();
        this.meterProvider?.Dispose();
    }

    [Benchmark]
    public async Task HttpGet()
    {
        using var httpResponse = await this.httpClient!.GetAsync(BaseAddress).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();
    }

    private async Task StartWebApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        app.MapGet("/", async context => await context.Response.WriteAsync("Hello World!"));

        await app.StartAsync();

        this.app = app;
        this.httpClient = app.GetTestClient();
    }
}
