// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Testcontainers.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public sealed class KafkaFixture : XunitContainerFixture<KafkaContainer>
{
    protected override string DockerfileName => "kafka.Dockerfile";

    protected override KafkaContainer CreateContainer() => new KafkaBuilder(this.GetImage()).Build();
}
