// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public class KafkaProducerStatisticsTestGroup
{
    public const string Name = "Kafka producer statistics";
}
