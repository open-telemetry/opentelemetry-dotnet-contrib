// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal class ConfluentKafkaMeterInstrumentation : IDisposable
{
    public const string InstrumentationName = "OpenTelemetry.Instrumentation.ConfluentKafka";

    private static readonly Dictionary<string, string> Descriptions = new()
    {
        { Gauges.ReplyQueue, "Number of ops (callbacks, events, etc) waiting in queue for application to serve with rd_kafka_poll()" },
        { Gauges.MessageCount, "Current number of messages in producer queues" },
        { Gauges.MessageSize, "Current total size of messages in producer queues" },
        { Counters.Tx, "Total number of requests sent to Kafka brokers" },
        { Counters.TxBytes, "Total number of bytes transmitted to Kafka brokers" },
        { Counters.Rx, "Total number of responses received from Kafka brokers" },
        { Counters.RxBytes, "Total number of bytes received from Kafka brokers" },
        { Counters.TxMessages, "Total number of messages transmitted (produced) to Kafka brokers" },
        { Counters.TxMessageBytes, "Total number of message bytes (including framing, such as per-Message framing and MessageSet/batch framing) transmitted to Kafka brokers" },
        { Counters.RxMessages, "Total number of messages consumed, not including ignored messages (due to offset, etc), from Kafka brokers" },
        { Counters.RxMessageBytes, "Total number of message bytes (including framing) received from Kafka brokers" },
    };

    private readonly Meter meter;
    private readonly MetricsChannel channel;
    private readonly Dictionary<string, Statistics> state = new();

    public ConfluentKafkaMeterInstrumentation(MetricsChannel channel)
    {
        this.channel = channel;
        this.meter = new(InstrumentationName);
        this.ReplyQueue = this.meter.CreateObservableGauge(Gauges.ReplyQueue, this.GetReplyQMeasurements, Descriptions[Gauges.ReplyQueue]);
        this.MessageCount = this.meter.CreateObservableGauge(Gauges.MessageCount, this.GetMessageCountMeasurements, Descriptions[Gauges.MessageCount]);
        this.MessageSize = this.meter.CreateObservableGauge(Gauges.MessageSize, this.GetMessageSizeMeasurements, Descriptions[Gauges.MessageSize]);
        this.Tx = this.meter.CreateCounter<long>(Counters.Tx, Descriptions[Counters.Tx]);
        this.TxBytes = this.meter.CreateCounter<long>(Counters.TxBytes, Descriptions[Counters.TxBytes]);
        this.TxMessages = this.meter.CreateCounter<long>(Counters.TxMessages, Descriptions[Counters.TxMessages]);
        this.TxMessageBytes = this.meter.CreateCounter<long>(Counters.TxMessageBytes, Descriptions[Counters.TxMessageBytes]);
        this.Rx = this.meter.CreateCounter<long>(Counters.Rx, Descriptions[Counters.Rx]);
        this.RxBytes = this.meter.CreateCounter<long>(Counters.RxBytes, Descriptions[Counters.RxBytes]);
        this.RxMessages = this.meter.CreateCounter<long>(Counters.RxMessages, Descriptions[Counters.RxMessages]);
        this.RxMessageBytes = this.meter.CreateCounter<long>(Counters.RxMessageBytes, Descriptions[Counters.RxMessageBytes]);
    }

    public ObservableGauge<long> ReplyQueue { get; }

    public ObservableGauge<long> MessageCount { get; }

    public ObservableGauge<long> MessageSize { get; }

    public Counter<long> Tx { get; }

    public Counter<long> TxBytes { get; }

    public Counter<long> TxMessages { get; }

    public Counter<long> TxMessageBytes { get; }

    public Counter<long> Rx { get; }

    public Counter<long> RxBytes { get; }

    public Counter<long> RxMessages { get; }

    public Counter<long> RxMessageBytes { get; }

    public ConcurrentQueue<Measurement<long>> ReplyQueueMeasurements { get; } = new ConcurrentQueue<Measurement<long>>();

    public ConcurrentQueue<Measurement<long>> MessageCountMeasurements { get; } = new ConcurrentQueue<Measurement<long>>();

    public ConcurrentQueue<Measurement<long>> MessageSizeMeasurements { get; } = new ConcurrentQueue<Measurement<long>>();

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await this.channel.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (this.channel.Reader.TryRead(out var json))
            {
                Statistics? statistics;
                try
                {
                    statistics = JsonSerializer.Deserialize(json, StatisticsJsonSerializerContext.Default.Statistics);
                }
                catch
                {
                    return;
                }

                if (statistics == null || statistics.Name == null)
                {
                    return;
                }

                TagList tags = new()
                {
                    { Tags.ClientId, statistics.ClientId },
                    { Tags.Name, statistics.Name },
                };

                this.ReplyQueueMeasurements.Enqueue(new Measurement<long>(statistics.ReplyQueue, tags));
                this.MessageCountMeasurements.Enqueue(new Measurement<long>(statistics.MessageCount, tags));
                this.MessageSizeMeasurements.Enqueue(new Measurement<long>(statistics.MessageSize, tags));

                tags.Add(new KeyValuePair<string, object?>(Tags.Type, statistics.Type));

                if (this.state.TryGetValue(statistics.Name, out var previous))
                {
                    this.Tx.Add(statistics.Tx - previous.Tx, tags);
                    this.TxBytes.Add(statistics.TxBytes - previous.TxBytes, tags);
                    this.TxMessages.Add(statistics.TxMessages - previous.TxMessages, tags);
                    this.TxMessageBytes.Add(statistics.TxMessageBytes - previous.TxMessageBytes, tags);
                    this.Rx.Add(statistics.Rx - previous.Rx, tags);
                    this.RxBytes.Add(statistics.RxBytes - previous.RxBytes, tags);
                    this.RxMessages.Add(statistics.RxMessages - previous.RxMessages, tags);
                    this.RxMessageBytes.Add(statistics.RxMessageBytes - previous.RxMessageBytes, tags);
                }
                else
                {
                    this.Tx.Add(statistics.Tx, tags);
                    this.TxBytes.Add(statistics.TxBytes, tags);
                    this.TxMessages.Add(statistics.TxMessages, tags);
                    this.TxMessageBytes.Add(statistics.TxMessageBytes, tags);
                    this.Rx.Add(statistics.Rx, tags);
                    this.RxBytes.Add(statistics.RxBytes, tags);
                    this.RxMessages.Add(statistics.RxMessages, tags);
                    this.RxMessageBytes.Add(statistics.RxMessageBytes, tags);
                }

                this.state[statistics.Name] = statistics;
            }
        }
    }

    public void Dispose()
    {
        this.meter?.Dispose();
    }

    private IEnumerable<Measurement<long>> GetReplyQMeasurements()
    {
        while (this.ReplyQueueMeasurements.TryDequeue(out var measurement))
        {
            yield return measurement;
        }
    }

    private IEnumerable<Measurement<long>> GetMessageCountMeasurements()
    {
        while (this.MessageCountMeasurements.TryDequeue(out var measurement))
        {
            yield return measurement;
        }
    }

    private IEnumerable<Measurement<long>> GetMessageSizeMeasurements()
    {
        while (this.MessageSizeMeasurements.TryDequeue(out var measurement))
        {
            yield return measurement;
        }
    }

    public static class Tags
    {
        public const string ClientId = "messaging.client_id";
        public const string Type = "type";
        public const string Name = "name";
    }

    private static class Gauges
    {
        public const string ReplyQueue = "messaging.kafka.consumer.queue.message_count";
        public const string MessageCount = "messaging.kafka.producer.queue.message_count";
        public const string MessageSize = "messaging.kafka.producer.queue.size";
    }

    private static class Counters
    {
        public const string Tx = "messaging.kafka.network.tx";
        public const string TxBytes = "messaging.kafka.network.transmitted";
        public const string Rx = "messaging.kafka.network.rx";
        public const string RxBytes = "messaging.kafka.network.received";
        public const string TxMessages = "messaging.publish.messages";
        public const string TxMessageBytes = "messaging.kafka.message.transmitted";
        public const string RxMessages = "messaging.receive.messages";
        public const string RxMessageBytes = "messaging.kafka.message.received";
    }
}
