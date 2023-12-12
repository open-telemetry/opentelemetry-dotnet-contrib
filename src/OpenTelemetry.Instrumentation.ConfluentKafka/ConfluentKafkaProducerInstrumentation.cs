// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal class ConfluentKafkaProducerInstrumentation : IDisposable
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
internal sealed class ConfluentKafkaProducerInstrumentation<TKey, TValue> : ConfluentKafkaProducerInstrumentation
#pragma warning restore SA1402 // File may only contain a single type
{
    private readonly InstrumentedProducerBuilder<TKey, TValue> producerBuilder;
    private readonly MetricsChannel channel;

    public ConfluentKafkaProducerInstrumentation(
        InstrumentedProducerBuilder<TKey, TValue> producerBuilder, MetricsChannel channel)
    {
        this.producerBuilder = producerBuilder;
        this.channel = channel;
        this.producerBuilder.SetStatisticsHandler(this.OnProducerStatistics);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    private void OnProducerStatistics(IProducer<TKey, TValue> producer, string json)
    {
        if (!this.producerBuilder.Options!.Metrics)
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
