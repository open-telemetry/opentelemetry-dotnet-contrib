// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Greet;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Benchmarks;

[MemoryDiagnoser]
public class AspNetCoreBenchmarks
{
    private static readonly Uri BaseAddress = new("/", UriKind.Relative);

    private readonly HelloRequest helloRequest = new();
    private HttpClient? httpClient;
    private Greeter.GreeterClient? grpcClient;
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

        KeyValuePair<string, string?>[] config =
        [
            new("OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION", "true"),
        ];

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Traces))
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices((services) => services.AddSingleton(configuration))
                .AddAspNetCoreInstrumentation()
                .Build();
        }

        if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Metrics))
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .ConfigureServices((services) => services.AddSingleton(configuration))
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

    [Benchmark]
    public async Task<HelloReply> GrpcGet() =>
        await this.grpcClient!.SayHelloAsync(this.helloRequest);

    private async Task StartWebApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        builder.Services.AddGrpc();

        var app = builder.Build();

        app.MapGet("/", async context => await context.Response.WriteAsync("Hello World!"));

        app.MapGrpcService<GreeterService>();

        await app.StartAsync();

        this.app = app;
        this.httpClient = app.GetTestClient();

        var channel = GrpcChannel.ForAddress("http://localhost", new()
        {
            HttpClient = this.httpClient,
        });

        this.grpcClient = new Greeter.GreeterClient(channel);
    }

    private sealed class GreeterService : Greeter.GreeterBase
    {
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context) =>
            Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
    }
}
