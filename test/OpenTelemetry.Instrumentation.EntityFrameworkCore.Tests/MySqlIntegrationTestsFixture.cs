// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Testcontainers.MySql;
using Xunit;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public sealed class MySqlIntegrationTestsFixture : IAsyncLifetime
{
    public MySqlContainer DatabaseContainer { get; } = CreateMySql();

    public Task InitializeAsync()
        => this.DatabaseContainer.StartAsync();

    public Task DisposeAsync()
        => this.DatabaseContainer.DisposeAsync().AsTask();

    private static MySqlContainer CreateMySql()
        => new MySqlBuilder().Build();
}
