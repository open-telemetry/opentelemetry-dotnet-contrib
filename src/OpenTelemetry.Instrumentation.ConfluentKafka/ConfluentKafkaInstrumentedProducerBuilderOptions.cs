// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Confluent.Kafka;

/// <summary>
/// Options for configuring telemetry on a <see cref="InstrumentedProducerBuilder{TKey, TValue}"/>
/// when creating an instrumented producer in code.
/// </summary>
public sealed class ConfluentKafkaInstrumentedProducerBuilderOptions
{
    private TimeSpan statisticsInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value indicating whether native producer client metrics should be enabled.
    /// These metrics are aggregated across enabled producers in the process and are independent of <see cref="EnableMetrics"/>.
    /// </summary>
    public bool EnableClientMetrics { get; set; }

    /// <summary>
    /// Gets or sets the statistics interval. The default is ten seconds.
    /// An explicitly configured <c>statistics.interval.ms</c>, including zero, takes precedence.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The interval is less than one millisecond or exceeds <see cref="int.MaxValue"/> milliseconds.</exception>
    public TimeSpan StatisticsInterval
    {
        get => this.statisticsInterval;
        set
        {
            if (value.TotalMilliseconds < 1 || value.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The interval must be between one and Int32.MaxValue milliseconds.");
            }

            this.statisticsInterval = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether metrics should be enabled for the producer.
    /// </summary>
    public bool EnableMetrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tracing should be enabled for the producer.
    /// </summary>
    public bool EnableTraces { get; set; }
}
