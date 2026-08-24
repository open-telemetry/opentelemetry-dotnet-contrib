// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Tests;

public class TraceSamplingRatePolicyTests
{
    public static TheoryData<double> ValidSamplingProbabilities =>
    [
        0,
        0.5,
        1,
    ];

    public static TheoryData<double> InvalidSamplingProbabilities =>
    [
        -double.Epsilon,
        1.0000000000000002,
        double.NaN,
        double.NegativeInfinity,
        double.PositiveInfinity,
    ];

    [Theory]
    [MemberData(nameof(ValidSamplingProbabilities))]
    public void TryCreate_WithValidInputs_ReturnsTrue(double samplingProbability)
    {
        var succeeded = TraceSamplingRatePolicy.TryCreate(
            "policy-id",
            "Policy name",
            samplingProbability,
            out var policy,
            out var error);

        Assert.True(succeeded);
        Assert.NotNull(policy);
        Assert.Null(error);
        Assert.Equal("policy-id", policy.Id);
        Assert.Equal("Policy name", policy.Name);
        Assert.Equal(samplingProbability, policy.SamplingProbability);
        Assert.Equal(TraceSamplingRatePolicy.PolicyTypeName, policy.PolicyType);
    }

    [Fact]
    public void TryCreate_WithNegativeZero_NormalizesToPositiveZero()
    {
        var succeeded = TraceSamplingRatePolicy.TryCreate(
            "policy-id",
            "Policy name",
            -0.0,
            out var policy,
            out _);

        Assert.True(succeeded);
        Assert.NotNull(policy);
        Assert.False(BitConverter.DoubleToInt64Bits(policy.SamplingProbability) < 0);
        Assert.Equal(0.0, policy.SamplingProbability);
    }

    [Fact]
    public void TryCreate_WithNullId_ReturnsFalse()
    {
        AssertInvalid(null, "Policy name", 0.5, "policy ID is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void TryCreate_WithEmptyOrWhitespaceId_ReturnsFalse(string id)
    {
        AssertInvalid(id, "Policy name", 0.5, "policy ID is required");
    }

    [Fact]
    public void TryCreate_WithNullName_ReturnsFalse()
    {
        AssertInvalid("policy-id", null, 0.5, "policy name is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void TryCreate_WithEmptyOrWhitespaceName_ReturnsFalse(string name)
    {
        AssertInvalid("policy-id", name, 0.5, "policy name is required");
    }

    [Theory]
    [MemberData(nameof(InvalidSamplingProbabilities))]
    public void TryCreate_WithInvalidSamplingProbability_ReturnsFalse(double samplingProbability)
    {
        AssertInvalid("policy-id", "Policy name", samplingProbability, "sampling probability");
    }

    [Fact]
    public void TryCreate_WithMultipleInvalidFields_ReportsFirstError()
    {
        // id is validated before name; the id error wins
        var succeeded = TraceSamplingRatePolicy.TryCreate(
            null,
            null,
            double.NaN,
            out _,
            out var error);

        Assert.False(succeeded);
        Assert.Contains("policy ID is required", error, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertInvalid(
        string? id,
        string? name,
        double samplingProbability,
        string expectedErrorFragment)
    {
        var succeeded = TraceSamplingRatePolicy.TryCreate(
            id,
            name,
            samplingProbability,
            out var policy,
            out var error);

        Assert.False(succeeded);
        Assert.Null(policy);
        Assert.NotNull(error);
        Assert.Contains(expectedErrorFragment, error, StringComparison.OrdinalIgnoreCase);
    }
}
