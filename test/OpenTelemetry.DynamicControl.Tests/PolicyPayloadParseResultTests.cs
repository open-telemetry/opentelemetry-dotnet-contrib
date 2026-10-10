// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyPayloadParseResultTests
{
    [Fact]
    public void Malformed_SetsErrorAndLeavesCollectionsEmpty()
    {
        var result = PolicyPayloadParseResult.Malformed("Broken.");

        Assert.True(result.IsMalformed, "The result should be identified as malformed.");
        Assert.Equal("Broken.", result.Error);
        Assert.Empty(result.Policies);
        Assert.Empty(result.Rejections);
        Assert.Empty(result.IgnoredKeys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Malformed_WithBlankError_Throws(string? error) =>
        Assert.ThrowsAny<ArgumentException>(() => PolicyPayloadParseResult.Malformed(error!));

    [Fact]
    public void Decoded_CarriesSuppliedCollections()
    {
        Assert.True(
            TraceSamplingRatePolicy.TryCreate(new PolicyId("id"), "Name", 0.5, out var policy, out _),
            "The policy should be successfully created.");

        ImmutableArray<TelemetryPolicy> policies = [policy];
        ImmutableArray<PolicyPayloadRejection> rejections =
        [
            new(PayloadEntryLocation.ForKey("key"), PolicyRejectionReason.InvalidValue, "Nope."),
        ];
        ImmutableArray<string> ignoredKeys = ["other"];

        var result = PolicyPayloadParseResult.Decoded(policies, rejections, ignoredKeys);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Null(result.Error);
        Assert.Equal(policies, result.Policies);
        Assert.Equal(rejections, result.Rejections);
        Assert.Equal(ignoredKeys, result.IgnoredKeys);
    }

    [Fact]
    public void Decoded_WithNoEntries_IsNotMalformed()
    {
        var result = PolicyPayloadParseResult.Decoded([], [], []);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Null(result.Error);
        Assert.Empty(result.Policies);
    }
}
