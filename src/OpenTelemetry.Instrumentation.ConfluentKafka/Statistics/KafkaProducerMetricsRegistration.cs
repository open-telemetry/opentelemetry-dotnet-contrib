// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class KafkaProducerMetricsRegistration : IDisposable
{
    private static readonly State DisposedState = new(null, 0, 0);

    private State state = new(null, 0, 0);

    public KafkaProducerMetricsRegistration()
    {
        KafkaProducerMetrics.Register(this);
    }

    public KafkaProducerStatistics? Snapshot => Volatile.Read(ref this.state).Snapshot;

    public void Update(string json)
    {
        var snapshot = KafkaProducerStatisticsParser.Parse(json);
        State previous;
        State next;
        do
        {
            previous = Volatile.Read(ref this.state);
            if (ReferenceEquals(previous, DisposedState)
                || (snapshot.Timestamp.HasValue && previous.Snapshot?.Timestamp is { } timestamp && snapshot.Timestamp.Value <= timestamp))
            {
                return;
            }

            // Keep counter baselines across snapshots in which an optional field is missing.
            next = new State(snapshot, snapshot.Records ?? previous.Records, snapshot.Bytes ?? previous.Bytes);
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref this.state, next, previous), previous));

        RecordDelta(KafkaProducerMetrics.Records, snapshot.Records, previous.Records);
        RecordDelta(KafkaProducerMetrics.Bytes, snapshot.Bytes, previous.Bytes);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref this.state, DisposedState);
        KafkaProducerMetrics.Unregister(this);
    }

    private static void RecordDelta(Counter<long> counter, long? current, long previous)
    {
        if (current.HasValue)
        {
            // The first snapshot contributes its total. A decrease starts a new native counter epoch.
            counter.Add(current.Value >= previous ? current.Value - previous : current.Value);
        }
    }

    private sealed class State
    {
        public State(KafkaProducerStatistics? snapshot, long records, long bytes)
        {
            this.Snapshot = snapshot;
            this.Records = records;
            this.Bytes = bytes;
        }

        public KafkaProducerStatistics? Snapshot { get; }

        public long Records { get; }

        public long Bytes { get; }
    }
}
