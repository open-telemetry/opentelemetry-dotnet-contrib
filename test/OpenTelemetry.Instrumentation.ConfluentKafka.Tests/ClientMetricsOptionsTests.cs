// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Confluent.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection(KafkaProducerStatisticsTestGroup.Name)]
public class ClientMetricsOptionsTests
{
    [Fact]
    public void ClientMetricsAreOptInAndIndependentOfOperationMetrics()
    {
        var defaults = new ConfluentKafkaInstrumentedProducerBuilderOptions();
        Assert.False(defaults.EnableClientMetrics);
        Assert.Equal(TimeSpan.FromSeconds(10), defaults.StatisticsInterval);

        var builder = new ProducerBuilder<string, string>(new ProducerConfig()).AsInstrumentedProducerBuilder(
            new ConfluentKafkaInstrumentedProducerBuilderOptions
            {
                EnableClientMetrics = true,
                StatisticsInterval = TimeSpan.FromSeconds(2),
            });
        Assert.True(builder.EnableClientMetrics);
        Assert.False(builder.EnableMetrics);
        Assert.False(builder.EnableTraces);
        Assert.Equal(TimeSpan.FromSeconds(2), builder.StatisticsInterval);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(86400000.0001)]
    public void InvalidStatisticsIntervalsAreRejected(double milliseconds)
    {
        var options = new ConfluentKafkaInstrumentedProducerBuilderOptions();

        // .NET Framework rounds FromMilliseconds to whole milliseconds.
        var interval = TimeSpan.FromTicks((long)(milliseconds * TimeSpan.TicksPerMillisecond));
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.StatisticsInterval = interval);
        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(86400000)]
    public void ValidStatisticsIntervalsAreAccepted(int milliseconds)
    {
        var options = new ConfluentKafkaInstrumentedProducerBuilderOptions
        {
            StatisticsInterval = TimeSpan.FromMilliseconds(milliseconds),
        };
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), options.StatisticsInterval);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("0", true)]
    [InlineData("1500", true)]
    [InlineData(null, false)]
    public void FailedBuildDoesNotChangeConfigurationOrHandler(string? configured, bool enabled)
    {
        // Invalid native configuration stops Build before creating a client or contacting a broker.
        var config = new List<KeyValuePair<string, string>> { new("invalid.option", "true") };
        if (configured != null)
        {
            config.Add(new("statistics.interval.ms", configured));
        }

        var builder = new ProducerBuilder<string, string>(config).AsInstrumentedProducerBuilder(
            new ConfluentKafkaInstrumentedProducerBuilderOptions
            {
                EnableClientMetrics = enabled,
                StatisticsInterval = TimeSpan.FromSeconds(2),
            });
        Action<IProducer<string, string>, string> userHandler = (_, _) => { };
        builder.SetStatisticsHandler(userHandler);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
        var actual = builder.GetInternalConfig()!.FirstOrDefault(pair => pair.Key == "statistics.interval.ms").Value;
        Assert.Same(config, builder.GetInternalConfig());
        Assert.Equal(configured, actual);
        Assert.Same(userHandler, builder.GetInternalStatisticsHandler());

        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Same(userHandler, builder.GetInternalStatisticsHandler());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BuildAcceptsEnumerableConfiguration(bool enableClientMetrics, bool explicitInterval)
    {
        var config = new List<KeyValuePair<string, string>>();
        if (explicitInterval)
        {
            config.Add(new("statistics.interval.ms", "0"));
        }

        var builder = new ProducerBuilder<string, string>(config).AsInstrumentedProducerBuilder(
            new ConfluentKafkaInstrumentedProducerBuilderOptions
            {
                EnableClientMetrics = enableClientMetrics,
                StatisticsInterval = TimeSpan.FromSeconds(1),
            });
        builder.SetLogHandler((_, _) => { });

        using var producer = builder.Build();
        Assert.IsType<InstrumentedProducer<string, string>>(producer);
        var actual = builder.GetInternalConfig()!.FirstOrDefault(pair => pair.Key == "statistics.interval.ms").Value;
        var expected = explicitInterval ? "0" : null;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ReusedBuilderReadsChangesToOriginalConfiguration()
    {
        var config = new ProducerConfig();
        var builder = new ProducerBuilder<string, string>(config).AsInstrumentedProducerBuilder(
            new ConfluentKafkaInstrumentedProducerBuilderOptions { EnableClientMetrics = true });
        builder.SetLogHandler((_, _) => { });

        using (builder.Build())
        {
            Assert.Null(config.StatisticsIntervalMs);
        }

        // The explicit value must be passed through rather than replaced by the default.
        config.StatisticsIntervalMs = 86400001;
        var exception = Assert.Throws<ArgumentException>(() => builder.Build());
        Assert.Contains("statistics.interval.ms", exception.Message);
    }

    [Fact]
    public async Task NativeStatisticsReachMetricsAndApplicationFailuresReachErrorHandler()
    {
        var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ConfluentKafkaCommon.Meter.Name && instrument.Name == "kafka.producer.buffer_total_bytes")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => observed.TrySetResult(true));
        listener.Start();

        var builder = new ProducerBuilder<string, string>(new ProducerConfig()).AsInstrumentedProducerBuilder(
            new ConfluentKafkaInstrumentedProducerBuilderOptions
            {
                EnableClientMetrics = true,
                StatisticsInterval = TimeSpan.FromMilliseconds(100),
            });
        builder.SetLogHandler((_, _) => { });
        var reported = new TaskCompletionSource<Error>(TaskCreationOptions.RunContinuationsAsynchronously);
        builder.SetErrorHandler((_, error) =>
        {
            if (error.Code == ErrorCode.Local_Application)
            {
                reported.TrySetResult(error);
            }
        });
        Action<IProducer<string, string>, string> userHandler = (_, _) => throw new InvalidOperationException("application statistics failure");
        builder.SetStatisticsHandler(userHandler);

        using var producer = builder.Build();
        Assert.Same(userHandler, builder.GetInternalStatisticsHandler());
        await KafkaProducerStatisticsProcessorTests.WaitForAsync(reported.Task);
        Assert.Contains("application statistics failure", (await reported.Task).Reason);
        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                listener.RecordObservableInstruments();
                return observed.Task.IsCompleted;
            },
            TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void CompositeHandlerPreservesUserCallbackWhenInstrumentationThrows()
    {
        var called = false;
        var handler = KafkaProducerStatisticsHandler.Create<string, string>(
            _ => throw new InvalidOperationException("instrumentation failure"),
            (_, json) =>
            {
                called = true;
                Assert.Equal("statistics", json);
            });

        handler(null!, "statistics");
        Assert.True(called);
    }

    [Fact]
    public void MalformedStatisticsRetainPreviousSnapshotAndInvokeUserCallback()
    {
        using var registration = new KafkaProducerMetricsRegistration();
        registration.Update(KafkaProducerStatisticsTests.ReadFixture("active"));
        var snapshot = registration.Snapshot;
        var called = false;
        var handler = KafkaProducerStatisticsHandler.Create<string, string>(registration.Update, (_, _) => called = true);

        handler(null!, "{");
        Assert.True(called);
        Assert.Same(snapshot, registration.Snapshot);
    }

    [Fact]
    public void UserCallbackFailuresPropagateAfterEnqueuingStatistics()
    {
        var updates = 0;
        var handler = KafkaProducerStatisticsHandler.Create<string, string>(
            _ => updates++,
            (_, _) => throw new InvalidOperationException("application failure"));

        Assert.Throws<InvalidOperationException>(() => handler(null!, "first"));
        Assert.Throws<InvalidOperationException>(() => handler(null!, "second"));
        Assert.Equal(2, updates);
    }
}
