// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.Tests;

public class LogLevelPolicyReaderTests
{
    public static TheoryData<string, string> AcceptedValues =>
        new()
        {
            { "\"trace\"", nameof(DiagnosticLogLevel.Trace) },
            { "\"debug\"", nameof(DiagnosticLogLevel.Debug) },
            { "\"info\"", nameof(DiagnosticLogLevel.Information) },
            { "\"information\"", nameof(DiagnosticLogLevel.Information) },
            { "\"warn\"", nameof(DiagnosticLogLevel.Warning) },
            { "\"warning\"", nameof(DiagnosticLogLevel.Warning) },
            { "\"error\"", nameof(DiagnosticLogLevel.Error) },
            { "\"none\"", nameof(DiagnosticLogLevel.None) },
            { "\"WARN\"", nameof(DiagnosticLogLevel.Warning) },
            { "\"Warn\"", nameof(DiagnosticLogLevel.Warning) },
            { "\" warn \"", nameof(DiagnosticLogLevel.Warning) },
            { "{\"level\": \"warn\"}", nameof(DiagnosticLogLevel.Warning) },
            { "{\"level\": \"warn\", \"future\": 1}", nameof(DiagnosticLogLevel.Warning) },
            { "{\"future\": 1, \"level\": \"warn\"}", nameof(DiagnosticLogLevel.Warning) },

            // Member matching operates on the unescaped property name.
            { "{\"\\u006Cevel\": \"warn\"}", nameof(DiagnosticLogLevel.Warning) },
        };

    public static TheoryData<string> InvalidShapes =>
    [
        "true",
        "false",
        "null",
        "[]",
        "[\"warn\"]",
        "{}",
        "{\"future\": 1}",
        "{\"Level\": \"warn\"}",
        "{\"level\": true}",
        "{\"level\": null}",
        "{\"level\": {}}",
        "{\"level\": []}",
        "{\"level\": \"warn\", \"level\": \"error\"}",
        "3",
        "{\"level\": 3}",
    ];

    public static TheoryData<string> InvalidValues =>
    [
        "\"critical\"",
        "\"verbose\"",
        "\"abc\"",
        "\"\"",
        "\" \"",
        "\"\\uD800\"",
        "{\"level\": \"abc\"}",
        "{\"level\": \"\\uD800\"}",
    ];

    [Theory]
    [MemberData(nameof(AcceptedValues))]
    public void Read_WithAcceptedValue_ReturnsPolicy(string json, string expectedLevel)
    {
        var policy = ReadPolicy(json);

        Assert.Equal(expectedLevel, policy.MinimumLevel.ToString());
    }

    [Fact]
    public void Read_SynthesizesIdentityFromThePolicyType()
    {
        var policy = ReadPolicy("\"warn\"");

        Assert.Equal(LogLevelPolicy.PolicyTypeValue, policy.PolicyType);
        Assert.Equal(new PolicyId(LogLevelPolicy.PolicyTypeValue.Value), policy.Id);
        Assert.Equal("Diagnostic log level", policy.Name);
        Assert.Equal(
            new PolicyKey(LogLevelPolicy.PolicyTypeValue, new PolicyId(LogLevelPolicy.PolicyTypeValue.Value)),
            PolicyKey.FromPolicy(policy));
    }

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public void Read_WithInvalidShape_ReportsShapeFailure(string json) =>
        AssertRejected(json, PolicyRejectionReason.SchemaMismatch);

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void Read_WithInvalidValue_ReportsValueFailure(string json) =>
        AssertRejected(json, PolicyRejectionReason.InvalidValue);

    [Fact]
    public void Read_WithTurkishCurrentCulture_MatchesTokensOrdinally()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal(DiagnosticLogLevel.Information, ReadPolicy("\"INFO\"").MinimumLevel);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Read_UsesTheSameLevelTokensAsTheSelfDiagnosticsEnvironmentVariable()
    {
        string[] tokens = ["error", "warn", "info", "debug", "trace", "none"];

        foreach (var token in tokens)
        {
            using var document = JsonDocument.Parse("\"" + token + "\"");

            Assert.True(LogLevelPolicyReader.Instance.Read(document.RootElement).TryGetPolicy(out _));
        }
    }

    private static LogLevelPolicy ReadPolicy(string json)
    {
        using var document = JsonDocument.Parse(json);

        var result = LogLevelPolicyReader.Instance.Read(document.RootElement);

        Assert.True(result.TryGetPolicy(out var policy), result.Error);
        Assert.Null(result.Error);
        Assert.Equal(PolicyRejectionReason.None, result.Reason);

        return Assert.IsType<LogLevelPolicy>(policy);
    }

    private static void AssertRejected(string json, PolicyRejectionReason expectedReason)
    {
        using var document = JsonDocument.Parse(json);

        var result = LogLevelPolicyReader.Instance.Read(document.RootElement);

        Assert.False(result.TryGetPolicy(out var policy));
        Assert.Null(policy);
        Assert.Equal(expectedReason, result.Reason);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }
}
