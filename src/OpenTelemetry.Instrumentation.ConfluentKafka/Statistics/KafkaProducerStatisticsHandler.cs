// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal static class KafkaProducerStatisticsHandler
{
    public static Action<IProducer<TKey, TValue>, string> Create<TKey, TValue>(
        Action<string> update,
        Action<IProducer<TKey, TValue>, string>? userHandler) => (producer, json) =>
    {
        try
        {
            update(json);
        }
        catch (Exception ex)
        {
            ConfluentKafkaInstrumentationEventSource.Log.StatisticsHandlerFailed(ex);
        }

        try
        {
            userHandler?.Invoke(producer, json);
        }
        catch (Exception ex)
        {
            ConfluentKafkaInstrumentationEventSource.Log.StatisticsHandlerFailed(ex);
        }
    };
}
