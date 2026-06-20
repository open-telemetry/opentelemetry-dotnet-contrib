// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Confluent.Kafka;

/// <summary>
/// Options for configuring telemetry on a <see cref="Confluent.Kafka.InstrumentedProducerBuilder{TKey, TValue}"/>
/// when creating an instrumented producer dynamically (outside DI).
/// </summary>
public sealed class ConfluentKafkaInstrumentedProducerBuilderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether metrics should be enabled for the producer.
    /// </summary>
    public bool EnableMetrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tracing should be enabled for the producer.
    /// </summary>
    public bool EnableTraces { get; set; }
}
