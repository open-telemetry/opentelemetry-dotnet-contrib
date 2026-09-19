// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck.Xunit;
using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public static class DiagnosticLogLevelParserTests
{
    private const int MaxTest = 1_000;

    [Property(MaxTest = MaxTest)]
    public static void TryParse_WithArbitraryText_UpholdsItsContract(string? text) =>
        AssertContract(text);

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void TryParse_WithNumericLookingText_UpholdsItsContract(NumericLookingText text) =>
        AssertContract(text.Text);

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(Generators)])]
    public static void TryParse_AcceptsEveryAcceptedTokenWhateverItsCase(AcceptedLogLevelToken token, bool padded)
    {
        var text = padded ? " " + token.Token + "\t" : token.Token;

        Assert.True(DiagnosticLogLevelParser.TryParse(text, out _), FuzzInput.Describe(text));
        AssertContract(text);
    }

    private static void AssertContract(string? text)
    {
        var parsed = DiagnosticLogLevelParser.TryParse(text, out var level);

        if (parsed)
        {
            Assert.NotEqual(DiagnosticLogLevel.Unspecified, level);
            Assert.True(
                LogLevelPolicy.TryCreate(new PolicyId("id"), "name", level, out _, out var error),
                $"{FuzzInput.Describe(text)} parsed to a level the model rejects: {error}");
        }
        else
        {
            Assert.Equal(DiagnosticLogLevel.Unspecified, level);
        }
    }
}
