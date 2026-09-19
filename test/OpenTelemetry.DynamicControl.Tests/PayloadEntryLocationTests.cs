// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Tests;

public class PayloadEntryLocationTests
{
    [Fact]
    public void ForKey_IdentifiesTheEntryByKey()
    {
        var location = PayloadEntryLocation.ForKey("sampling_rate");

        Assert.True(location.TryGetKey(out var key), "The location should identify the key.");
        Assert.Equal("sampling_rate", key);
        Assert.False(location.TryGetIndex(out _), "The location should not identify an index when it represents a key.");
        Assert.Equal("sampling_rate", location.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ForKey_WithBlankKey_Throws(string? key) =>
        Assert.ThrowsAny<ArgumentException>(() => PayloadEntryLocation.ForKey(key!));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue - 1)]
    [InlineData(int.MaxValue)]
    public void ForIndex_IdentifiesTheEntryByPosition(int index)
    {
        var location = PayloadEntryLocation.ForIndex(index);

        Assert.True(location.TryGetIndex(out var actual), "The location should identify the index.");
        Assert.Equal(index, actual);
        Assert.False(location.TryGetKey(out _), "The location should not identify a key when it represents an index.");
        Assert.Equal("[" + index.ToString(CultureInfo.InvariantCulture) + "]", location.ToString());
    }

    [Fact]
    public void ForIndex_AtTheLargestPosition_IsDistinctAndRetained()
    {
        // The position is held as given rather than offset, so the largest representable
        // index neither overflows into "no location" nor collides with any other position.
        var largest = PayloadEntryLocation.ForIndex(int.MaxValue);

        Assert.NotEqual(default, largest);
        Assert.NotEqual(PayloadEntryLocation.ForIndex(0), largest);
        Assert.NotEqual(PayloadEntryLocation.ForIndex(int.MaxValue - 1), largest);
        Assert.Equal(PayloadEntryLocation.ForIndex(int.MaxValue), largest);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ForIndex_WithNegativeIndex_Throws(int index) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PayloadEntryLocation.ForIndex(index));

    [Fact]
    public void Default_IdentifiesNeitherAKeyNorAPosition()
    {
        var location = default(PayloadEntryLocation);

        Assert.False(location.TryGetKey(out var key), "The default location should not identify a key.");
        Assert.Null(key);
        Assert.False(location.TryGetIndex(out var index), "The default location should not identify an index.");
        Assert.Equal(0, index);
        Assert.Equal("(none)", location.ToString());
    }

    [Fact]
    public void Default_IsNotTheSameAsPositionZero() =>
        Assert.NotEqual(default, PayloadEntryLocation.ForIndex(0));

    [Fact]
    public void Equals_IsTrueForMatchingKeys()
    {
        var left = PayloadEntryLocation.ForKey("sampling_rate");
        var right = PayloadEntryLocation.ForKey("sampling_rate");

        Assert.True(left.Equals(right), "Locations with matching keys should be equal.");
        Assert.True(left == right, "Locations with matching keys should be equal using ==");
        Assert.False(left != right, "Locations with matching keys should not be unequal using !=");
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_IsTrueForMatchingPositions()
    {
        var left = PayloadEntryLocation.ForIndex(3);
        var right = PayloadEntryLocation.ForIndex(3);

        Assert.True(left == right, "Locations with matching positions should be equal using ==");
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_IsOrdinal() =>
        Assert.NotEqual(PayloadEntryLocation.ForKey("sampling_rate"), PayloadEntryLocation.ForKey("SAMPLING_RATE"));

    [Fact]
    public void Equals_IsFalseAcrossKindsAndValues()
    {
        Assert.NotEqual(PayloadEntryLocation.ForKey("a"), PayloadEntryLocation.ForIndex(0));
        Assert.NotEqual(PayloadEntryLocation.ForIndex(0), PayloadEntryLocation.ForIndex(1));
        Assert.False(PayloadEntryLocation.ForKey("a").Equals("a"), "A location should not be equal to an object of a different type.");
    }

    [Fact]
    public void Equals_ViaObjectOverload_MatchesTypedOverload()
    {
        object boxed = PayloadEntryLocation.ForIndex(2);

        Assert.True(PayloadEntryLocation.ForIndex(2).Equals(boxed), "The object overload should match the typed overload for the same position.");
    }
}
