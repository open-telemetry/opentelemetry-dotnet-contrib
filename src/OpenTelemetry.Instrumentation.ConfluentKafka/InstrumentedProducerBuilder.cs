// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.ConfluentKafka;

namespace Confluent.Kafka;

/// <summary>
/// A builder of <see cref="IProducer{TKey,TValue}"/> with support for instrumentation.
/// </summary>
/// <typeparam name="TKey">Type of the key.</typeparam>
/// <typeparam name="TValue">Type of value.</typeparam>
public sealed class InstrumentedProducerBuilder<TKey, TValue> : ProducerBuilder<TKey, TValue>
{
    private readonly ConfluentKafkaProducerInstrumentationOptions<TKey, TValue> options = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentedProducerBuilder{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="config"> A collection of librdkafka configuration parameters (refer to https://github.com/edenhill/librdkafka/blob/master/CONFIGURATION.md) and parameters specific to this client (refer to: <see cref="ConfigPropertyNames" />). At a minimum, 'bootstrap.servers' must be specified.</param>
    public InstrumentedProducerBuilder(IEnumerable<KeyValuePair<string, string>> config)
        : base(config)
    {
    }

    internal bool EnableClientMetrics
    {
        get => this.options.ClientMetrics;
        set => this.options.ClientMetrics = value;
    }

    internal TimeSpan StatisticsInterval
    {
        get => this.options.StatisticsInterval;
        set => this.options.StatisticsInterval = value;
    }

    internal bool EnableMetrics
    {
        get => this.options.Metrics;
        set => this.options.Metrics = value;
    }

    internal bool EnableTraces
    {
        get => this.options.Traces;
        set => this.options.Traces = value;
    }

    /// <summary>
    /// Build a new IProducer instance.
    /// </summary>
    /// <returns>an <see cref="IProducer{TKey,TValue}"/>.</returns>
    public override IProducer<TKey, TValue> Build()
    {
        if (!this.options.ClientMetrics)
        {
            var bootstrapServers = GetBootstrapServers(this.Config);
            return new InstrumentedProducer<TKey, TValue>(base.Build(), this.options, bootstrapServers);
        }

        var nativeConfig = this.Config.ToList();
        if (!nativeConfig.Any(pair => string.Equals(pair.Key, "statistics.interval.ms", StringComparison.Ordinal)))
        {
            nativeConfig.Add(new KeyValuePair<string, string>(
                "statistics.interval.ms",
                ((int)this.options.StatisticsInterval.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var userHandler = this.StatisticsHandler;
        var registration = CreateMetricsRegistration();
        try
        {
            var handler = KafkaProducerStatisticsHandler.Create(registration.Enqueue, userHandler);
            var producer = new InstrumentedProducer<TKey, TValue>(
                this.BuildNativeProducer(nativeConfig, handler), this.options, GetBootstrapServers(nativeConfig), registration);
            registration = null;
            return producer;
        }
        finally
        {
            registration?.Dispose();
        }
    }

    private static KafkaProducerMetricsRegistration CreateMetricsRegistration() => new KafkaProducerMetricsRegistration();

    private static string? GetBootstrapServers(IEnumerable<KeyValuePair<string, string>> config)
        => config.LastOrDefault(pair => string.Equals(pair.Key, "bootstrap.servers", StringComparison.Ordinal)).Value;

    private IProducer<TKey, TValue> BuildNativeProducer(
        IEnumerable<KeyValuePair<string, string>> config,
        Action<IProducer<TKey, TValue>, string> statisticsHandler)
    {
        // Confluent.Kafka callbacks read their builder's handlers dynamically. Each client needs its own builder.
        var builder = new InstrumentedProducerBuilder<TKey, TValue>(config)
        {
            LogHandler = this.LogHandler,
            ErrorHandler = this.ErrorHandler,
            StatisticsHandler = statisticsHandler,
            OAuthBearerTokenRefreshHandler = this.OAuthBearerTokenRefreshHandler,
            Partitioners = this.Partitioners,
            DefaultPartitioner = this.DefaultPartitioner,
            KeySerializer = this.KeySerializer,
            ValueSerializer = this.ValueSerializer,
            AsyncKeySerializer = this.AsyncKeySerializer,
            AsyncValueSerializer = this.AsyncValueSerializer,
        };
        return builder.BuildNativeProducer();
    }

    private IProducer<TKey, TValue> BuildNativeProducer() => base.Build();
}
