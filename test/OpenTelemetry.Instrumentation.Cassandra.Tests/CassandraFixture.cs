// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Testcontainers.Cassandra;
using Xunit;

namespace OpenTelemetry.Instrumentation.Cassandra.Tests;

public sealed class CassandraFixture : IAsyncLifetime
{
    private static readonly string CassandraImage = GetCassandraImage();

    public CassandraContainer DatabaseContainer { get; } = CreateCassandra();

    public Task InitializeAsync() =>
        this.DatabaseContainer.StartAsync();

    public Task DisposeAsync() =>
        this.DatabaseContainer.DisposeAsync().AsTask();

    private static CassandraContainer CreateCassandra() =>
        new CassandraBuilder(CassandraImage).Build();

    private static string GetCassandraImage()
    {
        var assembly = typeof(CassandraFixture).Assembly;

        using var stream = assembly.GetManifestResourceStream("cassandra.Dockerfile");
        using var reader = new StreamReader(stream!);

        var raw = reader.ReadToEnd();

        // Exclude FROM
        return raw.Substring(4).Trim();
    }
}
