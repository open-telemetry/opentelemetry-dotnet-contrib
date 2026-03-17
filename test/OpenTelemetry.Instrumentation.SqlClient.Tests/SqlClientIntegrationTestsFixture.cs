// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Testcontainers.MsSql;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public sealed class SqlClientIntegrationTestsFixture : IAsyncLifetime
{
    private static readonly string SqlServerImage = GetSqlServerImage();

    public MsSqlContainer DatabaseContainer { get; } = CreateMsSql();

    public Task InitializeAsync() => this.DatabaseContainer.StartAsync();

    public Task DisposeAsync() => this.DatabaseContainer.DisposeAsync().AsTask();

    private static MsSqlContainer CreateMsSql()
        => new MsSqlBuilder(SqlServerImage).Build();

    private static string GetSqlServerImage()
    {
        var assembly = typeof(SqlClientIntegrationTestsFixture).Assembly;

#pragma warning disable IDE0370 // Suppression is unnecessary
        using var stream = assembly.GetManifestResourceStream("sqlserver.Dockerfile");
        using var reader = new StreamReader(stream!);
#pragma warning restore IDE0370 // Suppression is unnecessary

        var raw = reader.ReadToEnd();

        // Exclude FROM
        return raw.Substring(4).Trim();
    }
}
