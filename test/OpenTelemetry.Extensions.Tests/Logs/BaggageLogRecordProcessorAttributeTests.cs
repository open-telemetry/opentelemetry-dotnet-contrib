// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Extensions.Tests.Logs;

public class BaggageLogRecordProcessorAttributeTests
{
    [Fact]
    public void ApplicationAttributeTakesPrecedenceOverBaggageEntryWithTheSameKey()
    {
        var logRecord = LogWithBaggage(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UserId"] = "FROM-BAGGAGE",
                ["tenant"] = "contoso",
            },
            options => options.AddBaggageProcessor());

        Assert.NotNull(logRecord.Attributes);

        var userId = Assert.Single(logRecord.Attributes, kv => kv.Key == "UserId");
        Assert.Equal("LEGIT-USER", userId.Value);
        Assert.Contains(logRecord.Attributes, kv => kv.Key == "tenant" && (string?)kv.Value == "contoso");
    }

    [Fact]
    public void BaggageEntriesAreAppendedAfterTheRecordAttributes()
    {
        var logRecord = LogWithBaggage(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tenant"] = "contoso",
            },
            options => options.AddBaggageProcessor());

        Assert.NotNull(logRecord.Attributes);
        Assert.Equal(["UserId", "tenant"], logRecord.Attributes.Select(kv => kv.Key));
    }

    [Fact]
    public void RejectAllPredicateCopiesNoBaggage()
    {
        var logRecord = LogWithBaggage(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UserId"] = "FROM-BAGGAGE",
                ["session-token"] = "secret",
            },
            options => options.AddBaggageProcessor(_ => false));

        var attributes = logRecord.Attributes ?? [];

        Assert.DoesNotContain(attributes, kv => kv.Key == "session-token");
        Assert.Equal(["LEGIT-USER"], attributes.Where(kv => kv.Key == "UserId").Select(kv => (string?)kv.Value));
    }

    private static LogRecord LogWithBaggage(Dictionary<string, string> baggage, Action<OpenTelemetryLoggerOptions> configure)
    {
        var previousBaggage = Baggage.Current;

        try
        {
            Baggage.Current = Baggage.Create(baggage);

            var logRecords = new List<LogRecord>();
            using (var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    configure(options);
                    options.AddInMemoryExporter(logRecords);
                })
                .AddFilter("*", LogLevel.Trace)))
            {
                var logger = loggerFactory.CreateLogger(nameof(BaggageLogRecordProcessorAttributeTests));
                var state = new List<KeyValuePair<string, object?>> { new("UserId", "LEGIT-USER") };
                logger.Log(LogLevel.Information, default, state, null, (_, _) => "user performed an action");
            }

            return Assert.Single(logRecords);
        }
        finally
        {
            Baggage.Current = previousBaggage;
        }
    }
}
