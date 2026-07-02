// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.Internal;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class OtelTraceStateTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("vendor=value")] // No ot entry.
    [InlineData("ot=foo:bar")] // No th or rv sub-keys.
    public void Parse_WithoutThresholdOrRandomValue_HasNeither(string? traceState)
    {
        var state = OtelTraceState.Parse(traceState);

        Assert.False(state.HasThreshold);
        Assert.False(state.HasRandomValue);
    }

    [Theory]
    [InlineData("ot=th:0", 0L)]
    [InlineData("ot=th:8", 0x80000000000000L)] // 50%.
    [InlineData("ot=th:c", 0xc0000000000000L)] // 25%.
    [InlineData("ot=th:fd70a4", 0xfd70a400000000L)] // ~1%.
    public void Parse_ReadsThreshold(string traceState, long expected)
    {
        var state = OtelTraceState.Parse(traceState);

        Assert.True(state.HasThreshold);
        Assert.Equal(expected, state.Threshold);
    }

    [Fact]
    public void Parse_ReadsRandomValue()
    {
        // From https://opentelemetry.io/docs/specs/otel/trace/tracestate-handling/#explicit-randomness-value-rv
        var state = OtelTraceState.Parse("ot=rv:6e6d1a75832a2f");

        Assert.True(state.HasRandomValue);
        Assert.Equal(0x6e6d1a75832a2fL, state.RandomValue);
    }

    [Fact]
    public void Parse_ReadsThresholdAndRandomValue()
    {
        var state = OtelTraceState.Parse("ot=th:fd70a4;rv:6e6d1a75832a2f");

        Assert.Equal(0xfd70a400000000L, state.Threshold);
        Assert.Equal(0x6e6d1a75832a2fL, state.RandomValue);
    }

    [Theory]
    [InlineData("ot=th:")] // Empty value.
    [InlineData("ot=th:g")] // Not hexadecimal.
    [InlineData("ot=th:123456789012345")] // 15 digits, exceeds 14.
    public void Parse_IgnoresInvalidThreshold(string traceState)
        => Assert.False(OtelTraceState.Parse(traceState).HasThreshold);

    [Theory]
    [InlineData("ot=rv:6e6d1a75832a2")] // 13 digits.
    [InlineData("ot=rv:6e6d1a75832a2ff")] // 15 digits.
    [InlineData("ot=rv:6e6d1a75832axf")] // Not hexadecimal.
    public void Parse_IgnoresInvalidRandomValue(string traceState)
        => Assert.False(OtelTraceState.Parse(traceState).HasRandomValue);

    [Fact]
    public void Serialize_EmitsThresholdWithTrailingZerosRemoved()
    {
        var state = default(OtelTraceState);
        state.SetThreshold(0xfd70a400000000L);

        Assert.Equal("ot=th:fd70a4", state.Serialize());
    }

    [Fact]
    public void Serialize_EmitsRandomValueAs14Digits()
    {
        var state = default(OtelTraceState);
        state.SetRandomValue(0x6e6d1a75832a2fL);

        Assert.Equal("ot=rv:6e6d1a75832a2f", state.Serialize());
    }

    [Fact]
    public void Serialize_EmitsThresholdBeforeRandomValue()
    {
        var state = default(OtelTraceState);
        state.SetThreshold(0x80000000000000L);
        state.SetRandomValue(0x6e6d1a75832a2fL);

        Assert.Equal("ot=th:8;rv:6e6d1a75832a2f", state.Serialize());
    }

    [Fact]
    public void Serialize_WithNothingToEmit_ReturnsEmptyString()
        => Assert.Equal(string.Empty, default(OtelTraceState).Serialize());

    [Fact]
    public void ClearThreshold_RemovesThreshold()
    {
        var state = OtelTraceState.Parse("ot=th:8;rv:6e6d1a75832a2f");
        state.ClearThreshold();

        Assert.False(state.HasThreshold);
        Assert.Equal("ot=rv:6e6d1a75832a2f", state.Serialize());
    }

    [Fact]
    public void Serialize_PreservesOtherOtSubKeys()
    {
        var state = OtelTraceState.Parse("ot=th:8;foo:bar");

        Assert.Equal("ot=th:8;foo:bar", state.Serialize());
    }

    [Fact]
    public void Serialize_PreservesOtherTraceStateMembers()
    {
        var state = OtelTraceState.Parse("ot=th:8,vendor=value,other=123");

        Assert.Equal("ot=th:8,vendor=value,other=123", state.Serialize());
    }

    [Fact]
    public void Serialize_WithoutOtEntry_PreservesOtherMembers()
    {
        var state = OtelTraceState.Parse("vendor=value");
        state.SetThreshold(0x80000000000000L);

        Assert.Equal("ot=th:8,vendor=value", state.Serialize());
    }

    [Fact]
    public void Serialize_DropsSubKeysThatWouldExceedTheSizeLimit()
    {
        var large = new string('a', OtelTraceState.TraceStateSizeLimit);
        var state = OtelTraceState.Parse($"ot=th:8;foo:{large}");

        var serialized = state.Serialize();

        // The essential th sub-key is kept, the oversized foo sub-key is dropped.
        Assert.Equal("ot=th:8", serialized);
        Assert.True(serialized.Length <= OtelTraceState.TraceStateSizeLimit);
    }

    [Theory]
    [InlineData("ot=th:0")]
    [InlineData("ot=th:8;rv:6e6d1a75832a2f")]
    [InlineData("ot=th:fd70a4;rv:6e6d1a75832a2f")]
    public void ParseAndSerialize_RoundTrips(string traceState)
        => Assert.Equal(traceState, OtelTraceState.Parse(traceState).Serialize());

    [Fact]
    public void Parse_IgnoresEmptyMembers()
    {
        var state = OtelTraceState.Parse(",vendor=value");

        Assert.Equal("vendor=value", state.Serialize());
    }

    [Fact]
    public void Parse_PreservesMalformedMemberVerbatim()
    {
        var state = OtelTraceState.Parse("malformed,vendor=value");

        Assert.Equal("malformed,vendor=value", state.Serialize());
    }

    [Fact]
    public void Parse_IgnoresEmptyOtSubKeyPairs()
    {
        var state = OtelTraceState.Parse("ot=;th:8");

        Assert.True(state.HasThreshold);
        Assert.Equal("ot=th:8", state.Serialize());
    }

    [Fact]
    public void Parse_IgnoresMalformedOtSubKeyPair()
    {
        var state = OtelTraceState.Parse("ot=malformed;th:8");

        Assert.True(state.HasThreshold);
        Assert.Equal("ot=th:8", state.Serialize());
    }

    [Fact]
    public void Serialize_RemovesOtPrefixWhenOnlyOversizedSubKeysPresent()
    {
        var large = new string('a', OtelTraceState.TraceStateSizeLimit);
        var state = OtelTraceState.Parse($"ot=foo:{large}");

        Assert.Equal(string.Empty, state.Serialize());
    }
}
