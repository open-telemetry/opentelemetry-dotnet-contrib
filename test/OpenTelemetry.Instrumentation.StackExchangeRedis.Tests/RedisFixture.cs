// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Testcontainers.Redis;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Tests;

public class RedisFixture : ContainerFixture<RedisContainer>
{
    protected override string DockerfileName => "redis.Dockerfile";

    protected override RedisContainer CreateContainer() => new RedisBuilder(this.GetImage()).Build();
}
