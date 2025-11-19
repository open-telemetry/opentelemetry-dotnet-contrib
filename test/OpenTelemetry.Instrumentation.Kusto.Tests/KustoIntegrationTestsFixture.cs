// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Data;
using Testcontainers.Kusto;
using Xunit;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public sealed class KustoIntegrationTestsFixture : IAsyncLifetime
{
    private static readonly string KustoImage = GetKustoImage();

    public KustoContainer DatabaseContainer { get; } = CreateKusto();

    public KustoConnectionStringBuilder ConnectionStringBuilder => new(this.DatabaseContainer.GetConnectionString());

    public Task InitializeAsync() => this.DatabaseContainer.StartAsync();

    public Task DisposeAsync() => this.DatabaseContainer.DisposeAsync().AsTask();

    private static KustoContainer CreateKusto()
        => new KustoBuilder()
        .WithImage(KustoImage)
        .Build();

    private static string GetKustoImage()
    {
        var assembly = typeof(KustoIntegrationTestsFixture).Assembly;

        using var stream = assembly.GetManifestResourceStream("kusto.Dockerfile");
        using var reader = new StreamReader(stream!);

        var raw = reader.ReadToEnd();

        // Exclude FROM
        return raw.Substring(4).Trim();
    }
}
