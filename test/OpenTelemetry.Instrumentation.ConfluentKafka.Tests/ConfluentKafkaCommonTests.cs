// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class ConfluentKafkaCommonTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("message-key", "message-key")]
    [InlineData(42, "42")]
    [InlineData(true, "True")]
    public void FormatMessageKey_ReturnsCanonicalValue(object key, string expected)
    {
        var actual = ConfluentKafkaCommon.FormatMessageKey(key);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FormatMessageKey_UsesRoundTripDateTimeFormat()
    {
        var key = new DateTime(2026, 7, 7, 12, 34, 56, 789, DateTimeKind.Utc).AddTicks(1234);

        var actual = ConfluentKafkaCommon.FormatMessageKey(key);

        Assert.Equal("2026-07-07T12:34:56.7891234Z", actual);
    }

    [Fact]
    public void FormatMessageKey_OmitsKeyWithoutCanonicalFormat()
    {
        var actual = ConfluentKafkaCommon.FormatMessageKey(new DisplayOnlyKey());

        Assert.Null(actual);
    }

    [Fact]
    public void FormatMessageKey_DoesNotTruncateKeyAtMaximumLength()
    {
        var key = new string('a', ConfluentKafkaCommon.MaxMessageKeyLength);

        var actual = ConfluentKafkaCommon.FormatMessageKey(key);

        Assert.Same(key, actual);
    }

    [Fact]
    public void FormatMessageKey_TruncatesLongStringKey()
    {
        var key = new string('a', 900_000);

        var actual = ConfluentKafkaCommon.FormatMessageKey(key);

        Assert.Equal(new string('a', ConfluentKafkaCommon.MaxMessageKeyLength), actual);
    }

    [Fact]
    public void FormatMessageKey_DoesNotSplitSurrogatePairWhenTruncating()
    {
        // The emoji (a surrogate pair) straddles the maximum length.
        var key = new string('a', ConfluentKafkaCommon.MaxMessageKeyLength - 1) + "\U0001F600" + new string('b', 10);

        var actual = ConfluentKafkaCommon.FormatMessageKey(key);

        Assert.Equal(new string('a', ConfluentKafkaCommon.MaxMessageKeyLength - 1), actual);
    }

    private sealed class DisplayOnlyKey : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) => "display-value";
    }
}
