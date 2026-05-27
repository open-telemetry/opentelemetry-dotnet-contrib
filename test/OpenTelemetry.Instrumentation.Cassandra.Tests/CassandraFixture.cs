// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Testcontainers.Cassandra;

namespace OpenTelemetry.Instrumentation.Cassandra.Tests;

public sealed class CassandraFixture : XunitContainerFixture<CassandraContainer>
{
    protected override string DockerfileName => "cassandra.Dockerfile";

    protected override CassandraContainer CreateContainer() => new CassandraBuilder(this.GetImage()).Build();
}
