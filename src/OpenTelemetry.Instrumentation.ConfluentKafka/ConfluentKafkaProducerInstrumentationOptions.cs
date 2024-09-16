// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

/// <summary>
/// Options to configure instrumentation.
/// </summary>
/// <typeparam name="TKey">Type of the key.</typeparam>
/// <typeparam name="TValue">Type of value.</typeparam>
public class ConfluentKafkaProducerInstrumentationOptions<TKey, TValue>
{
    /// <summary>
    /// Gets or sets a value indicating whether metrics are enabled or not.
    /// </summary>
    public bool Metrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether traces are enabled or not.
    /// </summary>
    public bool Traces { get; set; }
}
