// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Testcontainers.PostgreSql;
using Xunit;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public sealed class PostgresIntegrationTestsFixture : IAsyncLifetime
{
    public PostgreSqlContainer DatabaseContainer { get; } = CreatePostgres();

    public Task InitializeAsync()
        => this.DatabaseContainer.StartAsync();

    public Task DisposeAsync()
        => this.DatabaseContainer.DisposeAsync().AsTask();

    private static PostgreSqlContainer CreatePostgres()
        => new PostgreSqlBuilder().Build();
}
