// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Tests;

public class LogLevelPolicyTests
{
    [Fact]
    public void TryCreate_WithSupportedLevel_Succeeds()
    {
        // Driven off the enum rather than a literal list: every member but the
        // Unspecified sentinel is expected to be a usable policy value, so adding a
        // member without teaching LogLevelPolicy.IsSupported about it fails here.
        foreach (var level in Enum.GetValues<DiagnosticLogLevel>())
        {
            if (level == DiagnosticLogLevel.Unspecified)
            {
                continue;
            }

            Assert.True(LogLevelPolicy.TryCreate(new PolicyId("policy-1"), "Policy one", level, out var policy, out var error));
            Assert.Null(error);
            Assert.NotNull(policy);
            Assert.Equal(new PolicyId("policy-1"), policy.Id);
            Assert.Equal("Policy one", policy.Name);
            Assert.Equal(level, policy.MinimumLevel);
            Assert.Equal(LogLevelPolicy.PolicyTypeValue, policy.PolicyType);
        }
    }

    [Fact]
    public void TryCreate_WithEmptyId_Fails()
    {
        Assert.False(LogLevelPolicy.TryCreate(PolicyId.Empty, "Policy one", DiagnosticLogLevel.Warning, out var policy, out var error));
        Assert.Null(policy);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void TryCreate_WithBlankName_Fails(string? name)
    {
        Assert.False(LogLevelPolicy.TryCreate(new PolicyId("policy-1"), name!, DiagnosticLogLevel.Warning, out var policy, out var error));
        Assert.Null(policy);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void TryCreate_WithUnsupportedLevel_Fails(int value)
    {
        // A cast from any integer produces a DiagnosticLogLevel, so an unsupported value
        // arrives as an ordinary value rather than as an error. Zero is the Unspecified
        // sentinel and is deliberately not a usable policy value.
        Assert.False(LogLevelPolicy.TryCreate(
            new PolicyId("policy-1"),
            "Policy one",
            (DiagnosticLogLevel)value,
            out var policy,
            out var error));

        Assert.Null(policy);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void DefaultLevel_IsNotSupported()
    {
        Assert.Equal(DiagnosticLogLevel.Unspecified, default);
        Assert.False(LogLevelPolicy.TryCreate(
            new PolicyId("policy-1"),
            "Policy one",
            default,
            out var policy,
            out var error));
        Assert.Null(policy);
        Assert.NotNull(error);
    }

    [Fact]
    public void PoliciesWithIdenticalContent_AreNotEqual()
    {
        Assert.True(LogLevelPolicy.TryCreate(new PolicyId("policy-1"), "Policy one", DiagnosticLogLevel.Warning, out var first, out _));
        Assert.True(LogLevelPolicy.TryCreate(new PolicyId("policy-1"), "Policy one", DiagnosticLogLevel.Warning, out var second, out _));

        Assert.NotSame(first, second);
        Assert.False(first.Equals(second));
    }
}
