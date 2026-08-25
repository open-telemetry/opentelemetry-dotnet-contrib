// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Tests;

public class DiagnosticLogLevelParserTests
{
    public static TheoryData<string, string> AcceptedTokens =>
        new()
        {
            { "trace", nameof(DiagnosticLogLevel.Trace) },
            { "debug", nameof(DiagnosticLogLevel.Debug) },
            { "info", nameof(DiagnosticLogLevel.Information) },
            { "information", nameof(DiagnosticLogLevel.Information) },
            { "warn", nameof(DiagnosticLogLevel.Warning) },
            { "warning", nameof(DiagnosticLogLevel.Warning) },
            { "error", nameof(DiagnosticLogLevel.Error) },
            { "none", nameof(DiagnosticLogLevel.None) },
        };

    public static TheoryData<string> RejectedTokens =>
    [
        "critical",
        "verbose",
        "abc",
        string.Empty,
        " ",
        "0",
        "3",
        "6",
    ];

    [Theory]
    [MemberData(nameof(AcceptedTokens))]
    public void TryParse_ReturnsMemberNamedByToken(string token, string expected)
    {
        Assert.True(DiagnosticLogLevelParser.TryParse(token, out var level));
        Assert.Equal(expected, level.ToString());
    }

    [Theory]
    [InlineData("WARN")]
    [InlineData("Warn")]
    [InlineData("wArN")]
    public void TryParse_IgnoresCase(string token)
    {
        Assert.True(DiagnosticLogLevelParser.TryParse(token, out var level));
        Assert.Equal(nameof(DiagnosticLogLevel.Warning), level.ToString());
    }

    [Theory]
    [InlineData(" warn")]
    [InlineData("warn ")]
    [InlineData("\t warn \r\n")]
    public void TryParse_IgnoresSurroundingWhiteSpace(string token)
    {
        Assert.True(DiagnosticLogLevelParser.TryParse(token, out var level));
        Assert.Equal(nameof(DiagnosticLogLevel.Warning), level.ToString());
    }

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public void TryParse_RejectsTokenNamingNoMember(string token)
    {
        Assert.False(DiagnosticLogLevelParser.TryParse(token, out var level));
        Assert.Equal(DiagnosticLogLevel.Unspecified, level);
    }

    [Fact]
    public void TryParse_RejectsNull()
    {
        Assert.False(DiagnosticLogLevelParser.TryParse(null, out var level));
        Assert.Equal(DiagnosticLogLevel.Unspecified, level);
    }

    [Fact]
    public void TryParse_AcceptsEveryTokenTheMessageNames()
    {
        foreach (var token in DiagnosticLogLevelParser.AcceptedTokenValues)
        {
            Assert.Contains($"'{token}'", DiagnosticLogLevelParser.AcceptedTokens);
            Assert.True(
                DiagnosticLogLevelParser.TryParse(token, out _),
                $"The message names '{token}', which the parser rejects.");
        }
    }

    [Fact]
    public void TryParse_ProducesMemberTheModelAccepts()
    {
        foreach (var entry in AcceptedTokens)
        {
            var token = (string)entry[0];

            Assert.True(DiagnosticLogLevelParser.TryParse(token, out var level));
            Assert.True(
                LogLevelPolicy.TryCreate(new PolicyId("id"), "name", level, out _, out var error),
                $"The token '{token}' parsed to a level the model rejects: {error}");
        }
    }
}
