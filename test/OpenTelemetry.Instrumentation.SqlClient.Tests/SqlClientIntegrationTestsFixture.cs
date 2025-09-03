// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Testcontainers.MsSql;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public sealed class SqlClientIntegrationTestsFixture : IAsyncLifetime
{
    public MsSqlContainer DatabaseContainer { get; } = CreateMsSql();

    public Task InitializeAsync() => this.DatabaseContainer.StartAsync();

    public Task DisposeAsync() => this.DatabaseContainer.DisposeAsync().AsTask();

    private static MsSqlContainer CreateMsSql()
        => new MsSqlBuilder()
               .WithImage("mcr.microsoft.com/mssql/server:2022-CU20-GDR1-ubuntu-22.04")
               .Build();
}
