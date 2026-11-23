// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

public class AdxJsonTests
{
    [Theory]
    [InlineData(0d, "0")]
    [InlineData(1d, "1")]
    [InlineData(-2.5d, "-2.5")]
    [InlineData(25d, "25")]
    [InlineData(0.1d, "0.1")]
    [InlineData(0.0001d, "0.0001")]
    [InlineData(0.00001d, "1e-05")]
    [InlineData(123456d, "123456")]
    [InlineData(1000000d, "1e+06")]
    [InlineData(1234567d, "1.234567e+06")]
    [InlineData(1.5e300d, "1.5e+300")]
    [InlineData(double.PositiveInfinity, "+Inf")]
    [InlineData(double.NegativeInfinity, "-Inf")]
    [InlineData(double.NaN, "NaN")]
    public void FormatGoFloat_MatchesGoStrconvShortestFormat(double value, string expected)
        => Assert.Equal(expected, AdxJson.FormatGoFloat(value));

    [Fact]
    public void FormatTimestamp_UsesRfc3339NanoStyle()
    {
        Assert.Equal("2024-01-02T03:04:05Z", AdxJson.FormatTimestamp(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
        Assert.Equal(
            "2024-01-02T03:04:05.123Z",
            AdxJson.FormatTimestamp(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddMilliseconds(123)));
        Assert.Equal(
            "2024-01-02T03:04:05.0000001Z",
            AdxJson.FormatTimestamp(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1)));
    }

    [Fact]
    public void FormatIds_EmptyForDefault()
    {
        Assert.Equal(string.Empty, AdxJson.FormatTraceId(default));
        Assert.Equal(string.Empty, AdxJson.FormatSpanId(default));

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        Assert.Equal(traceId.ToHexString(), AdxJson.FormatTraceId(traceId));
        Assert.Equal(spanId.ToHexString(), AdxJson.FormatSpanId(spanId));
    }
}
