// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Testcontainers.PostgreSql;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public sealed class PostgresIntegrationTestsFixture : XunitContainerFixture<PostgreSqlContainer>
{
    protected override string DockerfileName => "postgres.Dockerfile";

    protected override PostgreSqlContainer CreateContainer() => new PostgreSqlBuilder(this.GetImage()).Build();
}
