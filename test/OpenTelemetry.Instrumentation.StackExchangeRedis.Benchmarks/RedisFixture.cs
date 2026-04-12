// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Testcontainers.Redis;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Benchmarks;

public sealed class RedisFixture : IAsyncDisposable
{
    private static readonly string RedisImage = GetRedisImage();

    public RedisContainer DatabaseContainer { get; } = CreateRedis();

    public Task InitializeAsync() =>
        this.DatabaseContainer.StartAsync();

    public ValueTask DisposeAsync() =>
        this.DatabaseContainer.DisposeAsync();

    private static RedisContainer CreateRedis() =>
        new RedisBuilder(RedisImage).Build();

    private static string GetRedisImage()
    {
        var assembly = typeof(RedisFixture).Assembly;

        using var stream = assembly.GetManifestResourceStream("redis.Dockerfile");
        using var reader = new StreamReader(stream!);

        var raw = reader.ReadToEnd();

        // Exclude FROM
        return raw.Substring(4).Trim();
    }
}
