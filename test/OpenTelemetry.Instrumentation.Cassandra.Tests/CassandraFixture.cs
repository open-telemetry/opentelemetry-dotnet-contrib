// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Testcontainers.Cassandra;
using Xunit;

namespace OpenTelemetry.Instrumentation.Cassandra.Tests;

public sealed class CassandraFixture : IAsyncLifetime
{
    private static readonly string CassandraImage = GetCassandraImage();

    public CassandraContainer Container { get; } = CreateCassandra();

    public async Task InitializeAsync()
    {
        if (DockerHelper.IsAvailable(DockerPlatform.Linux))
        {
            await this.Container.StartAsync();
        }
    }

    public Task DisposeAsync() =>
        this.Container.DisposeAsync().AsTask();

    private static CassandraContainer CreateCassandra() =>
        new CassandraBuilder(CassandraImage).Build();

    private static string GetCassandraImage()
    {
        var assembly = typeof(CassandraFixture).Assembly;

        using var stream = assembly.GetManifestResourceStream("cassandra.Dockerfile");

#if NET
        using var reader = new StreamReader(stream!);
#else
        using var reader = new StreamReader(stream);
#endif

        var raw = reader.ReadToEnd();

        // Exclude FROM
        return raw.Substring(4).Trim();
    }
}
