// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Testcontainers.MySql;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public sealed class MySqlIntegrationTestsFixture : XunitContainerFixture<MySqlContainer>
{
    protected override string DockerfileName => "mysql.Dockerfile";

    protected override MySqlContainer CreateContainer() => new MySqlBuilder(this.GetImage()).Build();
}
