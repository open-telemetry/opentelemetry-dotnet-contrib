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
    /// parent instead of only linking to it. This is an opt-in behavior for single-message
    /// scenarios, as allowed by the messaging semantic conventions, which achieves a direct
    /// parent-child relationship between producer and consumer spans.
    /// When enabled, if message processing happens in the scope of another valid ambient
    /// context, the ambient context is added as a link on the <c>process</c> span to
    /// preserve the correlation between message processing and that context. If no
    /// (valid) creation context was extracted from the message headers, the <c>process</c>
    /// span is parented on that ambient context instead of being started as a new root span.
    /// The default value is <see langword="false" /> (the message creation context is
    /// used only as a span link, and the <c>process</c> span starts as a new root span
    /// when no creation context is present).
    /// </summary>
    public bool UseCreationContextAsParent { get; set; }
}
