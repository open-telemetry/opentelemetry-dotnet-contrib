// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public sealed class SqlClientIntegrationTestsFixture : IAsyncLifetime
{
    public IContainer DatabaseContainer { get; } = CreateMsSql();

    public Task InitializeAsync()
    {
        return this.DatabaseContainer.StartAsync();
    }

    public async Task DisposeAsync() => await this.DatabaseContainer.DisposeAsync();

    private static MsSqlContainer CreateMsSql()
    {
        // Note: This "WithImage" line can most likely be removed when
        // a new version (>4.5.0) of Testcontainers.MsSql released. See:
        // https://github.com/microsoft/mssql-docker/issues/881
        return new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-CU19-ubuntu-22.04")
            .Build();
    }
}
