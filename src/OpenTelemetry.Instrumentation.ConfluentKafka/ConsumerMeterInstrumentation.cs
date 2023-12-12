// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class ConsumerMeterInstrumentation : IDisposable
{
    private readonly Meter meter;
    private readonly Counter<long> receiveMessagesCounter;
    private readonly Histogram<double> receiveDurationHistogram;

    public ConsumerMeterInstrumentation()
    {
        this.meter = new Meter(ConfluentKafkaCommon.InstrumentationName, ConfluentKafkaCommon.InstrumentationVersion);
        this.receiveMessagesCounter = this.meter.CreateCounter<long>(SemanticConventions.MetricMessagingReceiveMessages);
        this.receiveDurationHistogram = this.meter.CreateHistogram<double>(SemanticConventions.MetricMessagingReceiveDuration);
    }

    public void RecordReceivedMessage(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.receiveMessagesCounter.Add(1, tags);
    }

    public void RecordReceiveDuration(double duration, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.receiveDurationHistogram.Record(duration, tags);
    }

    public void Dispose()
    {
        this.meter.Dispose();
    }
}
