// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;
using static OpenTelemetry.DynamicControl.Tests.PolicyKeyTestHelper;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyKeyComparerTests
{
    private static readonly PolicyKeyComparer Comparer = PolicyKeyComparer.Default;

    [Fact]
    public void Compare_DifferingType_OrderedByOrdinalType()
    {
        var smaller = Key("aaa", "id");
        var larger = Key("bbb", "id");
        Assert.True(Comparer.Compare(smaller, larger) < 0, "'aaa' type should sort before 'bbb' type");
        Assert.True(Comparer.Compare(larger, smaller) > 0, "'bbb' type should sort after 'aaa' type");
    }

    [Fact]
    public void Compare_SameTypeDifferingId_OrderedByOrdinalId()
    {
        var smaller = Key("type", "a-id");
        var larger = Key("type", "b-id");
        Assert.True(Comparer.Compare(smaller, larger) < 0, "'a-id' should sort before 'b-id' with the same type");
        Assert.True(Comparer.Compare(larger, smaller) > 0, "'b-id' should sort after 'a-id' with the same type");
    }

    [Fact]
    public void Compare_OrdinalNotCulture_UppercaseSortsFirst()
    {
        // Ordinal: 'B' (66) < 'a' (97)
        var upper = Key("type", "B");
        var lower = Key("type", "a");
        Assert.True(Comparer.Compare(upper, lower) < 0, "'B' should sort before 'a' in ordinal order");
        Assert.True(Comparer.Compare(lower, upper) > 0, "'a' should sort after 'B' in ordinal order");
    }

    [Fact]
    public void Compare_EmptyAgainstRealKey_OrderedDoesNotThrow()
    {
        var real = Key("type", "id");
        Assert.True(Comparer.Compare(PolicyKey.Empty, real) < 0, "empty key should sort before any real key");
        Assert.True(Comparer.Compare(real, PolicyKey.Empty) > 0, "real key should sort after empty key");
    }

    [Fact]
    public void Compare_IdenticalKeys_ReturnsZero()
    {
        var key = Key("trace-sampling", "policy-1");
        Assert.Equal(0, Comparer.Compare(key, key));
    }

    [Fact]
    public void Compare_EqualButDistinctInstances_ReturnsZero()
    {
        var x = Key("trace-sampling", "policy-1");
        var y = Key("trace-sampling", "policy-1");
        Assert.Equal(0, Comparer.Compare(x, y));
    }

    [Fact]
    public void Compare_EmptyAgainstEmpty_ReturnsZero() =>
        Assert.Equal(0, Comparer.Compare(PolicyKey.Empty, PolicyKey.Empty));

    [Fact]
    public void Instance_IsSingleton() =>
        Assert.Same(PolicyKeyComparer.Default, PolicyKeyComparer.Default);
}
