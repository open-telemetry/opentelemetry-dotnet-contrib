// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection(KafkaProducerStatisticsTestGroup.Name)]
public class KafkaProducerStatisticsTests
{
    [Fact]
    public void ActiveFixtureUsesClientTotalsAndSampleWeightedMilliseconds()
    {
        var snapshot = KafkaProducerStatisticsParser.Parse(ReadFixture("active"));

        Assert.Equal(10, snapshot.Records);
        Assert.Equal(1000, snapshot.Bytes);
        Assert.Equal(3, snapshot.RequestsInFlight);
        Assert.Equal(1000, snapshot.BufferTotalBytes);
        Assert.Equal(900, snapshot.BufferAvailableBytes);
        Assert.Equal(3, snapshot.QueueTimeSum / snapshot.QueueTimeCount);
        Assert.Equal(8, snapshot.ThrottleTimeSum / snapshot.ThrottleTimeCount);
        Assert.Equal(12, snapshot.ThrottleTimeMax);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("idle")]
    public void FixturesContainMappedFields(string state)
    {
        using var document = JsonDocument.Parse(ReadFixture(state));
        var root = document.RootElement;
        foreach (var field in new[] { "type", "ts", "txmsgs", "txmsg_bytes", "msg_size", "msg_size_max", "brokers" })
        {
            Assert.True(root.TryGetProperty(field, out _), $"Missing mapped field {field}.");
        }

        foreach (var broker in root.GetProperty("brokers").EnumerateObject())
        {
            Assert.True(broker.Value.TryGetProperty("waitresp_cnt", out _));
            foreach (var window in new[] { "int_latency", "throttle" })
            {
                var value = broker.Value.GetProperty(window);
                Assert.True(value.TryGetProperty("cnt", out _));
                Assert.True(value.TryGetProperty("sum", out _));
            }

            Assert.True(broker.Value.GetProperty("throttle").TryGetProperty("max", out _));
        }
    }

    [Theory]
    [InlineData("{\"type\":\"producer\"}")]
    [InlineData("{\"type\":\"producer\",\"txmsgs\":-1,\"txmsg_bytes\":\"bad\",\"msg_size_max\":null,\"brokers\":[]}")]
    public void MissingOrInvalidFieldsDoNotBecomeZero(string json)
    {
        var snapshot = KafkaProducerStatisticsParser.Parse(json);

        Assert.Null(snapshot.Records);
        Assert.Null(snapshot.Bytes);
        Assert.Null(snapshot.RequestsInFlight);
        Assert.Null(snapshot.BufferTotalBytes);
        Assert.Null(snapshot.BufferAvailableBytes);
        Assert.Equal(0, snapshot.QueueTimeCount);
        Assert.Equal(0, snapshot.ThrottleTimeCount);
        Assert.Null(snapshot.ThrottleTimeMax);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"type\":\"consumer\"}")]
    [InlineData("{\"type\":1}")]
    public void InvalidDocumentsAreRejected(string json) =>
        Assert.ThrowsAny<JsonException>(() => KafkaProducerStatisticsParser.Parse(json));

    [Fact]
    public void BootstrapRequestsAreIncludedWithoutBrokerLabels()
    {
        var snapshot = KafkaProducerStatisticsParser.Parse("""
            {"type":"producer","brokers":{"bootstrap":{"nodeid":-1,"waitresp_cnt":3},"invalid":null}}
            """);
        Assert.Equal(3, snapshot.RequestsInFlight);
    }

    [Fact]
    public void MissingWindowSumDoesNotProduceAnAverage()
    {
        var snapshot = KafkaProducerStatisticsParser.Parse("""
            {"type":"producer","brokers":{"a":{"int_latency":{"avg":10,"cnt":2},"throttle":{"max":7,"cnt":2}}}}
            """);
        Assert.Equal(0, snapshot.QueueTimeCount);
        Assert.Equal(0, snapshot.ThrottleTimeCount);
        Assert.Equal(7, snapshot.ThrottleTimeMax);
    }

    [Fact]
    public void InstrumentsHaveExpectedTypesUnitsAndNoAttributes()
    {
        using var listener = new MetricsListener();
        using var registration = new KafkaProducerMetricsRegistration();
        registration.Update(ReadFixture("active"));
        listener.Collect();

        Assert.IsType<Counter<long>>(listener.Instruments["kafka.producer.record_send_total"]);
        Assert.IsType<Counter<long>>(listener.Instruments["kafka.producer.byte_total"]);
        Assert.Equal("{record}", listener.Instruments["kafka.producer.record_send_total"].Unit);
        Assert.Equal("By", listener.Instruments["kafka.producer.byte_total"].Unit);
        Assert.Equal(3, listener.Gauge("kafka.producer.requests_in_flight"));
        Assert.Equal(1000, listener.Gauge("kafka.producer.buffer_total_bytes"));
        Assert.Equal(900, listener.Gauge("kafka.producer.buffer_available_bytes"));
        Assert.Equal(3, listener.Gauge("kafka.producer.record_queue_time_avg"));
        Assert.Equal(8, listener.Gauge("kafka.producer.produce_throttle_time_avg"));
        Assert.Equal(12, listener.Gauge("kafka.producer.produce_throttle_time_max"));
        Assert.Equal(8, listener.Instruments.Count);
        Assert.False(listener.HasAttributes);
        foreach (var name in new[] { "kafka.producer.record_queue_time_avg", "kafka.producer.produce_throttle_time_avg", "kafka.producer.produce_throttle_time_max" })
        {
            var instrument = Assert.IsType<ObservableGauge<double>>(listener.Instruments[name]);
            Assert.Equal("ms", instrument.Unit);
        }
    }

    [Fact]
    public void IdleWindowsAreAbsentButSampledZerosAreObserved()
    {
        using var listener = new MetricsListener();
        using var registration = new KafkaProducerMetricsRegistration();
        registration.Update(ReadFixture("idle"));
        listener.Collect();
        Assert.Null(listener.Gauge("kafka.producer.record_queue_time_avg"));
        Assert.Null(listener.Gauge("kafka.producer.produce_throttle_time_avg"));
        Assert.Null(listener.Gauge("kafka.producer.produce_throttle_time_max"));
        Assert.Equal(0, listener.Gauge("kafka.producer.requests_in_flight"));

        registration.Update("""
            {"type":"producer","ts":2000000,"brokers":{"a":{"int_latency":{"sum":0,"cnt":1},"throttle":{"sum":0,"max":0,"cnt":1}}}}
            """);
        listener.Collect();
        Assert.Equal(0, listener.Gauge("kafka.producer.record_queue_time_avg"));
        Assert.Equal(0, listener.Gauge("kafka.producer.produce_throttle_time_avg"));
        Assert.Equal(0, listener.Gauge("kafka.producer.produce_throttle_time_max"));
        Assert.Null(listener.Gauge("kafka.producer.buffer_total_bytes"));
    }

    [Fact]
    public void DuplicateClientIdsAggregateIndependentlyAndDisposalDoesNotSubtractCounters()
    {
        using var listener = new MetricsListener();
        using var first = new KafkaProducerMetricsRegistration();
        using var second = new KafkaProducerMetricsRegistration();
        first.Update(ReadFixture("active"));
        second.Update(ReadFixture("active"));
        listener.Collect();
        Assert.Equal(20, listener.Total("kafka.producer.record_send_total"));
        Assert.Equal(2000, listener.Total("kafka.producer.byte_total"));
        Assert.Equal(6, listener.Gauge("kafka.producer.requests_in_flight"));
        Assert.Equal(2000, listener.Gauge("kafka.producer.buffer_total_bytes"));

        first.Dispose();
        listener.Collect();
        Assert.Equal(3, listener.Gauge("kafka.producer.requests_in_flight"));
        Assert.Equal(20, listener.Total("kafka.producer.record_send_total"));

        second.Dispose();
        listener.Collect();
        Assert.Null(listener.Gauge("kafka.producer.requests_in_flight"));
        Assert.Equal(20, listener.Total("kafka.producer.record_send_total"));
    }

    [Fact]
    public void TimingIsSampleWeightedAcrossProducers()
    {
        using var listener = new MetricsListener();
        using var first = new KafkaProducerMetricsRegistration();
        using var second = new KafkaProducerMetricsRegistration();
        first.Update(ReadFixture("active"));
        second.Update("""
            {"type":"producer","brokers":{"a":{"int_latency":{"sum":11000,"cnt":1},"throttle":{"sum":16,"max":16,"cnt":1}}}}
            """);
        listener.Collect();
        Assert.Equal(5, listener.Gauge("kafka.producer.record_queue_time_avg"));
        Assert.Equal(10, listener.Gauge("kafka.producer.produce_throttle_time_avg"));
        Assert.Equal(16, listener.Gauge("kafka.producer.produce_throttle_time_max"));
    }

    [Fact]
    public void CountersHandleRepeatedSnapshotsMissingFieldsResetsAndRecreation()
    {
        using var listener = new MetricsListener();
        using (var registration = new KafkaProducerMetricsRegistration())
        {
            registration.Update("{\"type\":\"producer\",\"txmsgs\":10,\"txmsg_bytes\":100}");
            registration.Update("{\"type\":\"producer\",\"txmsgs\":10,\"txmsg_bytes\":100}");
            registration.Update("{\"type\":\"producer\"}");
            registration.Update("{\"type\":\"producer\",\"txmsgs\":15,\"txmsg_bytes\":150}");
            registration.Update("{\"type\":\"producer\",\"txmsgs\":2,\"txmsg_bytes\":20}");
            registration.Update("{\"type\":\"producer\",\"txmsgs\":4,\"txmsg_bytes\":40}");
        }

        using var recreated = new KafkaProducerMetricsRegistration();
        recreated.Update("{\"type\":\"producer\",\"txmsgs\":3,\"txmsg_bytes\":30}");
        Assert.Equal(22, listener.Total("kafka.producer.record_send_total"));
        Assert.Equal(220, listener.Total("kafka.producer.byte_total"));
    }

    [Fact]
    public void TimestampRejectsStaleSnapshots()
    {
        using var listener = new MetricsListener();
        using var registration = new KafkaProducerMetricsRegistration();
        registration.Update("{\"type\":\"producer\",\"ts\":2,\"txmsgs\":10}");
        registration.Update("{\"type\":\"producer\",\"ts\":1,\"txmsgs\":3}");
        registration.Update("{\"type\":\"producer\",\"ts\":3,\"txmsgs\":12}");
        Assert.Equal(12, listener.Total("kafka.producer.record_send_total"));
    }

    [Fact]
    public async Task ConcurrentDisposalCannotResurrectObservations()
    {
        using var listener = new MetricsListener();
        using var registration = new KafkaProducerMetricsRegistration();
        var json = ReadFixture("active");
        await Task.WhenAll(
            Task.Run(
                () =>
                {
                    for (var i = 0; i < 1000; i++)
                    {
                        registration.Update(json);
                    }
                },
                TestContext.Current.CancellationToken),
            Task.Run(registration.Dispose, TestContext.Current.CancellationToken));
        listener.Collect();
        Assert.Null(registration.Snapshot);
        Assert.Null(listener.Gauge("kafka.producer.buffer_total_bytes"));
        var total = listener.Total("kafka.producer.record_send_total");
        registration.Update(json);
        Assert.Equal(total, listener.Total("kafka.producer.record_send_total"));
    }

    internal static string ReadFixture(string state) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Statistics", "Fixtures", $"librdkafka-2.4.0-producer-{state}.json"));

    private sealed class MetricsListener : IDisposable
    {
        private readonly MeterListener listener = new();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> measurements = new();
        private int hasAttributes;

        public MetricsListener()
        {
            this.listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == ConfluentKafkaCommon.Meter.Name && instrument.Name.StartsWith("kafka.producer.", StringComparison.Ordinal))
                {
                    this.Instruments[instrument.Name] = instrument;
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            this.listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => this.Record(instrument, measurement, tags.Length));
            this.listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => this.Record(instrument, measurement, tags.Length));
            this.listener.Start();
        }

        public ConcurrentDictionary<string, Instrument> Instruments { get; } = new();

        public bool HasAttributes => Volatile.Read(ref this.hasAttributes) != 0;

        public double Total(string name) => this.measurements.TryGetValue(name, out var values) ? values.Sum() : 0;

        public double? Gauge(string name) => this.measurements.TryGetValue(name, out var values) && !values.IsEmpty ? values.Last() : null;

        public void Collect()
        {
            foreach (var pair in this.measurements)
            {
                if (this.Instruments[pair.Key] is not Counter<long>)
                {
                    while (pair.Value.TryDequeue(out _))
                    {
                        // Drain previous gauge observations before collecting again.
                    }
                }
            }

            this.listener.RecordObservableInstruments();
        }

        public void Dispose() => this.listener.Dispose();

        private void Record(Instrument instrument, double value, int attributeCount)
        {
            if (attributeCount != 0)
            {
                Interlocked.Exchange(ref this.hasAttributes, 1);
            }

            this.measurements.GetOrAdd(instrument.Name, _ => new ConcurrentQueue<double>()).Enqueue(value);
        }
    }
}
