// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class ProducerMeterInstrumentation : IDisposable
{
    private readonly Meter meter;
    private readonly Counter<long> publishMessagesCounter;
    private readonly Histogram<double> publishDurationHistogram;

    public ProducerMeterInstrumentation()
    {
        this.meter = new Meter(ConfluentKafkaCommon.InstrumentationName, ConfluentKafkaCommon.InstrumentationVersion);
        this.publishMessagesCounter = this.meter.CreateCounter<long>(SemanticConventions.MetricMessagingPublishMessages);
        this.publishDurationHistogram = this.meter.CreateHistogram<double>(SemanticConventions.MetricMessagingPublishDuration);
    }

    public void RecordPublishMessage(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.publishMessagesCounter.Add(1, tags);
    }

    public void RecordPublishDuration(double duration, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.publishDurationHistogram.Record(duration, tags);
    }

    public void Dispose()
    {
        this.meter.Dispose();
    }
}
