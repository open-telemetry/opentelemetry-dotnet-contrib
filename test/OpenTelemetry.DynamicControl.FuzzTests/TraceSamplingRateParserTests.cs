// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using FsCheck.Xunit;
using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public static class TraceSamplingRateParserTests
{
    private const int MaxTest = 1_000;

    [Property(MaxTest = MaxTest)]
    public static void TryParse_WithArbitraryText_UpholdsItsContract(string? text) =>
        AssertContract(text);

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void TryParse_WithNumericLookingText_UpholdsItsContract(NumericLookingText text) =>
        AssertContract(text.Text);

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void TryParse_ReadsBackAnyNonNegativeNumberItCanSpell(NonNegativeFiniteDouble raw)
    {
        var expected = raw.Value;
        var text = expected.ToString("G17", CultureInfo.InvariantCulture);

        Assert.True(TraceSamplingRateParser.TryParse(text, out var probability), text);
        Assert.Equal(expected, probability);

        AssertContract(text);
    }

    private static void AssertContract(string? text)
    {
        var parsed = TraceSamplingRateParser.TryParse(text, out var probability);

        if (parsed)
        {
            Assert.False(double.IsNaN(probability), FuzzInput.Describe(text));
            Assert.False(double.IsInfinity(probability), FuzzInput.Describe(text));
            Assert.True(probability >= 0, FuzzInput.Describe(text));

            if (probability <= 1)
            {
                Assert.True(
                    TraceSamplingRatePolicy.TryCreate(new PolicyId("id"), "name", probability, out _, out var error),
                    $"{FuzzInput.Describe(text)} parsed to {probability}, which the model rejects: {error}");
            }
        }
        else
        {
            Assert.Equal(default, probability);
        }
    }
}
