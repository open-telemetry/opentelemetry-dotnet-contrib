// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;
using Testcontainers.SqlEdge;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public sealed class SqlClientIntegrationTestsFixture : IAsyncLifetime
{
    // The Microsoft SQL Server Docker image is not compatible with ARM devices, such as Macs with Apple Silicon.
    // See https://github.com/microsoft/mssql-docker/issues/881.
    public IContainer DatabaseContainer { get; } = Architecture.Arm64.Equals(RuntimeInformation.ProcessArchitecture) ? CreateSqlEdge() : CreateMsSql();

    public Task InitializeAsync()
    {
        return this.DatabaseContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return this.DatabaseContainer.DisposeAsync().AsTask();
    }

    private static SqlEdgeContainer CreateSqlEdge()
    {
        // Note: The Testcontainers.SqlEdge package has been deprecated. Seems
        // it will not work with newer GitHub-hosted runners. Need to find an
        // alternative solution. See:
        // https://github.com/testcontainers/testcontainers-dotnet/pull/1265
        return new SqlEdgeBuilder().Build();
    }

    private static MsSqlContainer CreateMsSql()
    {
        // Note: This "WithImage" line can most likely be removed when there is
        // a new version (>3.10.0) of Testcontainers.MsSql released. See:
        // https://github.com/testcontainers/testcontainers-dotnet/pull/1265
        return new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-CU20-ubuntu-22.04")
            .Build();
    }
}
