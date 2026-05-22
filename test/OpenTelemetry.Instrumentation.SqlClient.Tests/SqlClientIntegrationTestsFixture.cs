// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Testcontainers.MsSql;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public sealed class SqlClientIntegrationTestsFixture : XunitContainerFixture<MsSqlContainer>
{
    protected override string DockerfileName => "sqlserver.Dockerfile";

    protected override MsSqlContainer CreateContainer() => new MsSqlBuilder(this.GetImage()).Build();
}
