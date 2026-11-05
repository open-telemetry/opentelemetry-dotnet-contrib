// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Confluent.Kafka;

/// <summary>
/// Options for configuring telemetry on a <see cref="InstrumentedConsumerBuilder{TKey, TValue}"/>
/// when creating an instrumented producer in code.
/// </summary>
public sealed class ConfluentKafkaInstrumentedConsumerBuilderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether metrics should be enabled for the consumer.
    /// </summary>
    public bool EnableMetrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tracing should be enabled for the consumer.
    /// </summary>
    public bool EnableTraces { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>process</c> span created when
    /// processing a message with <c>ConsumeAndProcessMessageAsync</c>
    /// should use the message creation context (extracted from the message headers) as its
    /// parent instead of only linking to it. The default value is <see langword="false" />.
    /// </summary>
    public bool UseCreationContextAsParent { get; set; }
}
