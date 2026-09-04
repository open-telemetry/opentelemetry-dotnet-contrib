// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyReadResultTests
{
    [Fact]
    public void Success_CarriesThePolicyAndNoFailure()
    {
        var policy = CreatePolicy();

        var result = PolicyReadResult.Success(policy);

        Assert.True(result.TryGetPolicy(out var read));
        Assert.Same(policy, read);
        Assert.Equal(PolicyRejectionReason.None, result.Reason);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Reject_CarriesTheFailureAndNoPolicy()
    {
        var result = PolicyReadResult.Reject(PolicyRejectionReason.InvalidValue, "Nope.");

        Assert.False(result.TryGetPolicy(out var read));
        Assert.Null(read);
        Assert.Equal(PolicyRejectionReason.InvalidValue, result.Reason);
        Assert.Equal("Nope.", result.Error);
    }

    [Fact]
    public void Success_WithNullPolicy_Throws() =>
        Assert.Throws<ArgumentNullException>(() => PolicyReadResult.Success(null!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Reject_WithBlankError_Throws(string? error) =>
        Assert.ThrowsAny<ArgumentException>(() => PolicyReadResult.Reject(PolicyRejectionReason.InvalidValue, error!));

    [Fact]
    public void Reject_WithoutAReason_Throws() =>
        Assert.Throws<ArgumentException>(() => PolicyReadResult.Reject(PolicyRejectionReason.None, "Nope."));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Reject_WithoutAReasonAndABlankError_ReportsTheMissingReason(string? error)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => PolicyReadResult.Reject(PolicyRejectionReason.None, error!));

        Assert.Equal("reason", exception.ParamName);
    }

    private static TraceSamplingRatePolicy CreatePolicy()
    {
        Assert.True(TraceSamplingRatePolicy.TryCreate(new PolicyId("id"), "name", 0.5, out var policy, out _));

        return policy;
    }
}
