// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class KafkaProducerMetricsRegistration : IDisposable
{
    private static readonly State DisposedState = new(null, null);

    private readonly KafkaProducerStatisticsProcessor processor;
    private State state = new(null, new());

    public KafkaProducerMetricsRegistration()
    {
        this.processor = new KafkaProducerStatisticsProcessor(this.Update);
        KafkaProducerMetrics.Register(this);
    }

    public KafkaProducerStatistics? Snapshot => Volatile.Read(ref this.state).Snapshot;

    public Task Completion => this.processor.Completion;

    public void Enqueue(string json) => this.processor.TryEnqueue(json);

    public void Update(string json)
    {
        var snapshot = KafkaProducerStatisticsParser.Parse(json);
        var previous = Volatile.Read(ref this.state);
        if (previous.Baselines is not { } baselines
            || (snapshot.Timestamp.HasValue && previous.Snapshot?.Timestamp is { } timestamp && snapshot.Timestamp.Value <= timestamp))
        {
            return;
        }

        // Only the channel reader updates baselines. Disposal swaps out the state without waiting.
        var next = new State(snapshot, baselines);
        if (!ReferenceEquals(Interlocked.CompareExchange(ref this.state, next, previous), previous))
        {
            return;
        }

        long? records = null;
        long? bytes = null;
        foreach (var partition in snapshot.Partitions)
        {
            var key = (partition.Topic, partition.Partition);
            baselines.TryGetValue(key, out var baseline);
            AddDelta(ref records, partition.Records, baseline.Records);
            AddDelta(ref bytes, partition.Bytes, baseline.Bytes);

            // Retain baselines even if a partition or field is missing from a later snapshot.
            baselines[key] = (partition.Records ?? baseline.Records, partition.Bytes ?? baseline.Bytes);
        }

        if (records.HasValue)
        {
            KafkaProducerMetrics.Records.Add(records.Value);
        }

        if (bytes.HasValue)
        {
            KafkaProducerMetrics.Bytes.Add(bytes.Value);
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref this.state, DisposedState);
        this.processor.Dispose();
        KafkaProducerMetrics.Unregister(this);
    }

    private static void AddDelta(ref long? total, long? current, long? previous)
    {
        if (current.HasValue)
        {
            // The first observation or a decrease starts a new epoch for this partition counter.
            var delta = previous is { } baseline && current.Value >= baseline ? current.Value - baseline : current.Value;
            total = checked((total ?? 0) + delta);
        }
    }

    private sealed class State
    {
        public State(
            KafkaProducerStatistics? snapshot,
            Dictionary<(string Topic, int Partition), (long? Records, long? Bytes)>? baselines)
        {
            this.Snapshot = snapshot;
            this.Baselines = baselines;
        }

        public KafkaProducerStatistics? Snapshot { get; }

        public Dictionary<(string Topic, int Partition), (long? Records, long? Bytes)>? Baselines { get; }
    }
}
