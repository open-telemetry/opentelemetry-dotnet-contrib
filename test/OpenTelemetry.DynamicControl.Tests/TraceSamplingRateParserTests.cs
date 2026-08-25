// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Tests;

public class TraceSamplingRateParserTests
{
    public static TheoryData<string, double> AcceptedText =>
        new()
        {
            { "0", 0d },
            { "1", 1d },
            { "0.5", 0.5d },
            { ".5", 0.5d },
            { "0.25", 0.25d },
            { "1e-3", 0.001d },
            { "1E-3", 0.001d },
            { "+0.5", 0.5d },

            // Upper-bound validation remains the policy model's responsibility.
            { "2", 2d },
        };

    public static TheoryData<string> RejectedText =>
    [
        string.Empty,
        " ",
        "abc",
        "0.5.5",
        "1/2",
        "50%",
        "0x1",
        "-0",
        "-0.0",
        "-1",
        "-1e-4000",
        "1e-4000",
        "1e400",
        "NaN",
        "Infinity",

        // Invariant rules only: a decimal comma is a display convention, not a wire form.
        "0,5",

        // Grouping is a presentation device that NumberStyles.Float excludes.
        "1,000",
    ];

    [Theory]
    [MemberData(nameof(AcceptedText))]
    public void TryParse_ReturnsNumberTheTextSpells(string text, double expected)
    {
        Assert.True(TraceSamplingRateParser.TryParse(text, out var probability));
        Assert.Equal(expected, probability);
    }

    [Theory]
    [InlineData(" 0.5")]
    [InlineData("0.5 ")]
    [InlineData("\t 0.5 \r\n")]
    public void TryParse_IgnoresSurroundingWhiteSpace(string text)
    {
        Assert.True(TraceSamplingRateParser.TryParse(text, out var probability));
        Assert.Equal(0.5d, probability);
    }

    [Theory]
    [MemberData(nameof(RejectedText))]
    public void TryParse_RejectsTextSpellingNoNumber(string text)
    {
        Assert.False(TraceSamplingRateParser.TryParse(text, out var probability));
        Assert.Equal(default, probability);
    }

    [Fact]
    public void TryParse_RejectsNull()
    {
        Assert.False(TraceSamplingRateParser.TryParse(null, out var probability));
        Assert.Equal(default, probability);
    }

    [Fact]
    public void TryParse_DoesNotDependOnCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.True(TraceSamplingRateParser.TryParse("0.5", out var probability));
            Assert.Equal(0.5d, probability);

            Assert.False(TraceSamplingRateParser.TryParse("0,5", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
