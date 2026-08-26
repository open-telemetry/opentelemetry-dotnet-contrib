// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.Tests;

public class TraceSamplingRatePolicyReaderTests
{
    public static TheoryData<string, double> AcceptedValues =>
        new()
        {
            { "0.5", 0.5 },
            { "\"0.5\"", 0.5 },
            { "\" 0.5 \"", 0.5 },
            { "\"5e-1\"", 0.5 },
            { "0", 0 },
            { "1", 1 },
            { "{\"probability\": 0.5}", 0.5 },
            { "{\"probability\": \"0.5\"}", 0.5 },
            { "{\"probability\": 0.5, \"future\": 1}", 0.5 },
            { "{\"future\": 1, \"probability\": 0.5}", 0.5 },

            // Member matching operates on the unescaped property name.
            { "{\"\\u0070robability\": 0.5}", 0.5 },
        };

    public static TheoryData<string> InvalidShapes =>
    [
        "true",
        "false",
        "null",
        "[]",
        "[0.5]",
        "{}",
        "{\"future\": 1}",
        "{\"Probability\": 0.5}",
        "{\"probability\": true}",
        "{\"probability\": null}",
        "{\"probability\": {}}",
        "{\"probability\": []}",
        "{\"probability\": 0.1, \"probability\": 0.2}",
    ];

    public static TheoryData<string> InvalidValues =>
    [
        "1.5",
        "-0",
        "-0.0",
        "-0.1",
        "-1e-4000",
        "1e-4000",
        "1e400",
        "-1e400",
        "\"abc\"",
        "\"\"",
        "\" \"",
        "\"-0\"",
        "\"-0.0\"",
        "\"-1e-4000\"",
        "\"1e-4000\"",
        "\"NaN\"",
        "\"Infinity\"",
        "\"-Infinity\"",
        "{\"probability\": 1.5}",
        "{\"probability\": -0.0}",
        "{\"probability\": -1e-4000}",
        "{\"probability\": 1e-4000}",
        "{\"probability\": \"abc\"}",
        "{\"probability\": \"-0.0\"}",
        "{\"probability\": \"-1e-4000\"}",
        "{\"probability\": \"1e-4000\"}",
    ];

    [Theory]
    [MemberData(nameof(AcceptedValues))]
    public void Read_WithAcceptedValue_ReturnsPolicy(string json, double expectedProbability)
    {
        var policy = ReadPolicy(json);

        Assert.Equal(expectedProbability, policy.SamplingProbability);
    }

    [Fact]
    public void Read_SynthesizesIdentityFromThePolicyType()
    {
        var policy = ReadPolicy("0.5");

        Assert.Equal(TraceSamplingRatePolicy.PolicyTypeValue, policy.PolicyType);
        Assert.Equal(new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value), policy.Id);
        Assert.Equal("Trace sampling rate", policy.Name);
        Assert.Equal(
            new PolicyKey(
                TraceSamplingRatePolicy.PolicyTypeValue,
                new PolicyId(TraceSamplingRatePolicy.PolicyTypeValue.Value)),
            PolicyKey.FromPolicy(policy));
    }

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public void Read_WithInvalidShape_ReportsShapeFailure(string json)
    {
        AssertRejected(json, PolicyRejectionReason.SchemaMismatch);
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void Read_WithInvalidValue_ReportsValueFailure(string json)
    {
        AssertRejected(json, PolicyRejectionReason.InvalidValue);
    }

    [Fact]
    public void Read_WithCommaDecimalCurrentCulture_ParsesStringInvariantly()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal(0.5, ReadPolicy("\"0.5\"").SamplingProbability);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Read_WithCommaDecimalString_IsRejected()
    {
        AssertRejected("\"0,5\"", PolicyRejectionReason.InvalidValue);
    }

    [Fact]
    public void Read_WithUnreadableEscapedString_ReportsValueFailure()
    {
        AssertRejected("\"\\uD800\"", PolicyRejectionReason.InvalidValue);
    }

    [Fact]
    public void Read_WithUnreadableEscapedProbabilityString_ReportsValueFailure()
    {
        AssertRejected("{\"probability\": \"\\uD800\"}", PolicyRejectionReason.InvalidValue);
    }

    private static TraceSamplingRatePolicy ReadPolicy(string json)
    {
        using var document = JsonDocument.Parse(json);

        var result = TraceSamplingRatePolicyReader.Instance.Read(document.RootElement);

        Assert.True(result.TryGetPolicy(out var policy), result.Error);
        Assert.Null(result.Error);
        Assert.Equal(PolicyRejectionReason.None, result.Reason);

        return Assert.IsType<TraceSamplingRatePolicy>(policy);
    }

    private static void AssertRejected(string json, PolicyRejectionReason expectedReason)
    {
        using var document = JsonDocument.Parse(json);

        var result = TraceSamplingRatePolicyReader.Instance.Read(document.RootElement);

        Assert.False(result.TryGetPolicy(out var policy));
        Assert.Null(policy);
        Assert.Equal(expectedReason, result.Reason);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }
}
