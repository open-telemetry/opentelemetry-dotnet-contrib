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
    private readonly IContainer databaseContainer = Architecture.Arm64.Equals(RuntimeInformation.ProcessArchitecture) ? new SqlEdgeBuilder().Build() : new MsSqlBuilder().Build();

    public IContainer DatabaseContainer => this.databaseContainer;

    public Task InitializeAsync()
    {
        return this.databaseContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return this.databaseContainer.DisposeAsync().AsTask();
    }
}
