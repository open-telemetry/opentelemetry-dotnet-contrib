// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    [InlineData(2147483648)]
    public void InvalidStatisticsIntervalsAreRejected(double milliseconds)
    {
        var options = new ConfluentKafkaInstrumentedProducerBuilderOptions();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.StatisticsInterval = TimeSpan.FromMilliseconds(milliseconds));
        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void ValidStatisticsIntervalsAreAccepted(int milliseconds)
    {
        var options = new ConfluentKafkaInstrumentedProducerBuilderOptions
        {
            StatisticsInterval = TimeSpan.FromMilliseconds(milliseconds),
        };
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), options.StatisticsInterval);
    }

    [Theory]
    [InlineData(null, true, "2000")]
    [InlineData("0", true, "0")]
    [InlineData("1500", true, "1500")]
    [InlineData(null, false, null)]
    public void BuildRespectsExplicitConfigurationAndOptIn(string? configured, bool enabled, string? expected)
    {
        // Invalid native configuration stops Build before creating a client or contacting a broker.
        // The statistics configuration and handler restoration happen before that failure.
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
        Assert.Equal(expected, actual);
        Assert.Same(userHandler, builder.GetInternalStatisticsHandler());

        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Same(userHandler, builder.GetInternalStatisticsHandler());
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
    public void UserCallbackFailuresCannotEscapeOrDisableFutureUpdates()
    {
        var updates = 0;
        var handler = KafkaProducerStatisticsHandler.Create<string, string>(
            _ => updates++,
            (_, _) => throw new InvalidOperationException("application failure"));

        handler(null!, "first");
        handler(null!, "second");
        Assert.Equal(2, updates);
    }
}
