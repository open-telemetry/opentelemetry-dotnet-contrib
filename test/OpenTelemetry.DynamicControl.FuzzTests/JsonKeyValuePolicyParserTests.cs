// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck.Xunit;
using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.FuzzTests;

public static class JsonKeyValuePolicyParserTests
{
    private const int MaxTest = 1_000;

    [Property(MaxTest = MaxTest)]
    public static void Parse_WithArbitraryBytes_AlwaysReturnsAWellFormedResult(byte[]? payload)
    {
        var result = JsonKeyValuePolicyParser.Parse(payload ?? []);

        if (result.IsMalformed)
        {
            Assert.NotNull(result.Error);
            Assert.NotEmpty(result.Error);
            Assert.Empty(result.Policies);
            Assert.Empty(result.Rejections);
            Assert.Empty(result.IgnoredKeys);
            return;
        }

        Assert.Null(result.Error);

        foreach (var policy in result.Policies)
        {
            Assert.False(policy.PolicyType.IsEmpty);
            Assert.False(policy.Id.IsEmpty);
            Assert.NotNull(policy.Name);
        }

        foreach (var rejection in result.Rejections)
        {
            Assert.NotEqual(PolicyRejectionReason.None, rejection.Reason);
            Assert.NotEmpty(rejection.Message);
        }
    }
}
