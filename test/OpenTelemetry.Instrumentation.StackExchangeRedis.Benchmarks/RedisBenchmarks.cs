// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Benchmarks;

[MemoryDiagnoser]
public class RedisBenchmarks
{
    private ConnectionMultiplexer? connection;
    private IDatabase? database;
    private RedisFixture? redis;
    private TracerProvider? tracerProvider;

    [Params(false, true)]
    public bool EnableTracing { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        this.redis = new RedisFixture();
        await this.redis.InitializeAsync().ConfigureAwait(false);

        var connectionString = this.redis.DatabaseContainer.GetConnectionString();
        this.connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
        this.database = this.connection.GetDatabase();

        KeyValuePair<string, string?>[] config =
        [
            new("OTEL_SEMCONV_STABILITY_OPT_IN", "database"),
        ];

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        if (this.EnableTracing)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices((services) => services.AddSingleton(configuration))
                .AddRedisInstrumentation(this.connection)
                .Build();
        }
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (this.connection is not null)
        {
            await this.connection.CloseAsync().ConfigureAwait(false);
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }

        this.tracerProvider?.Dispose();

        if (this.redis is not null)
        {
            await this.redis.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Benchmark]
    public bool KeyExists() => this.database!.KeyExists("list");

    [Benchmark]
    public async Task<bool> KeyExistsAsync() => await this.database!.KeyExistsAsync("list");
}
