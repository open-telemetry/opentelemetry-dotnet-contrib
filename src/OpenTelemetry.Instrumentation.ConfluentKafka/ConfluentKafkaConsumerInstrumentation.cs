// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal class ConfluentKafkaConsumerInstrumentation : IDisposable
{
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // TODO release managed resources here
        }
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal sealed class ConfluentKafkaConsumerInstrumentation<TKey, TValue> : ConfluentKafkaConsumerInstrumentation
#pragma warning restore SA1402 // File may only contain a single type
{
    private readonly InstrumentedConsumerBuilder<TKey, TValue> consumerBuilder;
    private readonly MetricsChannel channel;

    public ConfluentKafkaConsumerInstrumentation(InstrumentedConsumerBuilder<TKey, TValue> consumerBuilder, MetricsChannel channel)
    {
        this.consumerBuilder = consumerBuilder;
        this.channel = channel;
        this.consumerBuilder.SetStatisticsHandler(this.OnConsumerStatistics);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    private void OnConsumerStatistics(IConsumer<TKey, TValue> consumer, string json)
    {
        if (!this.consumerBuilder.Options!.Metrics)
        {
            return;
        }

        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        this.channel!.Writer.TryWrite(json);
    }
}
