// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class W3CTraceStateTests
{
    // "There can be a maximum of 32 list-members in a list."
    // https://www.w3.org/TR/2021/REC-trace-context-1-20211123/#tracestate-header
    private const int MaxMembers = 32;

    // key = ( lcalpha / DIGIT ) 0*255 ( keychar ), so 256 characters is the maximum.
    private const int MaxKeyLength = 256;

    // value = 0*255(chr) nblk-chr, so 256 characters is the maximum.
    private const int MaxValueLength = 256;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("malformed")]
    [InlineData("=novalue")]
    [InlineData(",,,")]
    [InlineData("vendora=1,")]
    [InlineData("VENDORA=1")]
    public void Parse_WithAnyInput_DoesNotThrow(string? tracestate)
        => Assert.Null(Record.Exception(() => W3CTraceState.Parse(tracestate)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseAndToString_WithNothingToEmit_ReturnsEmptyString(string? tracestate)
        => Assert.Equal(string.Empty, W3CTraceState.Parse(tracestate).ToString());

    [Theory]
    [InlineData("vendora=1, vendorb=2")]
    [InlineData("vendora=1,\tvendorb=2")]
    [InlineData(" vendora=1 , vendorb=2 ")]
    public void Parse_IgnoresWhitespaceSurroundingMembers(string tracestate)
        => Assert.Equal("vendora=1,vendorb=2", W3CTraceState.Parse(tracestate).ToString());

    [Theory]
    [InlineData("vendora=1\u00A0")]
    [InlineData("\r\nvendora=1")]
    [InlineData("vendora=1\u000B")]
    public void Parse_TrimsOnlySpaceAndHorizontalTab(string tracestate)
    {
        // "Spaces and horizontal tabs surrounding list-members are ignored"; any other whitespace is
        // part of the member, which then fails the key or value grammar and is dropped.
        Assert.Equal(string.Empty, W3CTraceState.Parse(tracestate).ToString());
    }

    [Theory]
    [InlineData(",vendora=1,vendorb=2")]
    [InlineData("vendora=1,,vendorb=2")]
    [InlineData("vendora=1,vendorb=2,")]
    [InlineData("vendora=1, ,vendorb=2")]
    public void Parse_DropsEmptyMembers(string tracestate)
        => Assert.Equal("vendora=1,vendorb=2", W3CTraceState.Parse(tracestate).ToString());

    [Fact]
    public void Parse_DropsAMalformedMember()
    {
        // "Invalid tracestate entries MAY also be discarded", and a TraceState "MUST at all times be
        // valid", so a member that does not parse as a key/value pair is dropped rather than re-emitted.
        Assert.Equal("vendora=1,vendorb=2", W3CTraceState.Parse("vendora=1,malformed,vendorb=2").ToString());
    }

    [Fact]
    public void Parse_DropsADuplicateKeyAndKeepsTheFirst()
    {
        // "Only one entry per key is allowed", so a repeated key makes the later member invalid. The
        // first occurrence is the left-most, which is the most recently written position.
        Assert.Equal("vendora=1,vendorb=2", W3CTraceState.Parse("vendora=1,vendorb=2,vendora=3").ToString());
    }

    [Fact]
    public void ParseAndToString_WithHeaderLargerThanTheRecommendedMinimum_RoundTripsIntact()
    {
        // 512 characters is a floor on what vendors SHOULD be able to propagate, not a ceiling on
        // what this type emits, so a header well past it is returned intact.
        var tracestate = string.Join(
            ",",
            Enumerable.Range(0, 4).Select(static index => $"vendor{index}={new string('a', 250)}"));

        Assert.Equal(tracestate, W3CTraceState.Parse(tracestate).ToString());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData(",,,", true)]
    [InlineData("vendora=1", true)]
    [InlineData("vendora=1,,vendorb=2", true)]
    [InlineData("vendora=1,malformed", false)]
    [InlineData("malformed,vendora=1", false)]
    [InlineData("vendora=1,vendora=2", false)]
    [InlineData("malformed", false)]
    [InlineData("VENDORA=1", false)]
    [InlineData("=novalue", false)]
    [InlineData("vendora=", false)]
    [InlineData("malformed,alsomalformed", false)]
    public void TryParse_ReturnsFalseWhenAnyMemberIsDiscarded(string? tracestate, bool expected)
        => Assert.Equal(expected, W3CTraceState.TryParse(tracestate, out _));

    [Theory]
    [InlineData("malformed")]
    [InlineData("VENDORA=1")]
    [InlineData("=novalue")]
    [InlineData("malformed,alsomalformed")]
    public void TryParse_WhenNoMemberMatchesTheGrammar_YieldsTheEmptyState(string tracestate)
    {
        // Every member was discarded, so the state is populated the same way it is on the true branch
        // and there is nothing left to emit.
        Assert.False(W3CTraceState.TryParse(tracestate, out var state));

        Assert.Equal(string.Empty, state.ToString());
    }

    [Fact]
    public void TryParse_WhenSomeMembersAreInvalid_YieldsOnlyTheValidOnes()
    {
        // The report says a member was discarded; the state carries the members that were not.
        Assert.False(W3CTraceState.TryParse("vendora=1,malformed,vendorb=2", out var state));

        Assert.Equal("vendora=1,vendorb=2", state.ToString());
    }

    [Fact]
    public void TryParse_WithMoreMembersThanTheMax_KeepsAtMostTheMax()
    {
        var incomingMembers = Enumerable.Range(0, MaxMembers * 2)
                                        .Select(static index => $"vendor{index}=value")
                                        .ToArray();

        Assert.True(W3CTraceState.TryParse(string.Join(",", incomingMembers), out var state));

        var outgoingMembers = state.ToString().Split(',');

        Assert.Equal(MaxMembers, outgoingMembers.Length);
        Assert.Equal(incomingMembers.Take(MaxMembers), outgoingMembers);
    }

    [Fact]
    public void TryParse_DoesNotCountDiscardedMembersTowardMaxMembers()
    {
        // A discarded member never takes a slot, so a well-formed pair sitting after 32 of them is
        // still taken on. The report is false because members were discarded, not because it was.
        var incomingMembers = Enumerable.Range(0, MaxMembers)
                                        .Select(static index => $"malformed{index}")
                                        .Concat(["vendora=1"])
                                        .ToArray();

        Assert.False(W3CTraceState.TryParse(string.Join(",", incomingMembers), out var state));

        Assert.Equal("vendora=1", state.ToString());
    }

    [Fact]
    public void Set_WhenKeyExists_MovesItToTheFrontAndPreservesTheOtherOrder()
    {
        var state = W3CTraceState.Parse("vendora=1,vendorb=2,mykey=old");

        Assert.Equal("mykey=new,vendora=1,vendorb=2", state.Set("mykey", "new").ToString());
    }

    [Fact]
    public void Set_WhenKeyIsNew_AddsItToTheFrontAndPreservesTheOtherOrder()
    {
        var state = W3CTraceState.Parse("vendora=1,vendorb=2,vendorc=3,vendord=4");

        Assert.Equal("mykey=v,vendora=1,vendorb=2,vendorc=3,vendord=4", state.Set("mykey", "v").ToString());
    }

    [Fact]
    public void Set_WhenIncomingKeyIsWhitespacePadded_DoesNotProduceTheKeyTwice()
    {
        var state = W3CTraceState.Parse("vendora=1, mykey=old");

        Assert.Equal("mykey=new,vendora=1", state.Set("mykey", "new").ToString());
    }

    [Fact]
    public void Parse_DropsAMemberWithAnEmptyValue()
    {
        // A value is at least one character, so a member carrying none is invalid and is dropped
        // rather than kept as text; setting the key afterwards then produces it exactly once.
        var state = W3CTraceState.Parse("vendora=");

        Assert.Equal(string.Empty, state.ToString());
        Assert.Equal("vendora=1", state.Set("vendora", "1").ToString());
    }

    [Fact]
    public void Set_WithAnOpenTelemetryEntryPresent_TreatsItAsAnOrdinaryMember()
    {
        // The type is W3C-general: the ot key gets no precedence of its own here, unlike the
        // ot-scoped serializer the consistent probability sampler uses.
        var state = W3CTraceState.Parse("ot=th:8,vendora=1");

        Assert.Equal("mykey=v,ot=th:8,vendora=1", state.Set("mykey", "v").ToString());
    }

    [Fact]
    public void Set_DoesNotMutateTheReceiver()
    {
        var state = W3CTraceState.Parse("vendora=1");

        var updated = state.Set("mykey", "v");

        Assert.NotSame(state, updated);
        Assert.Equal("vendora=1", state.ToString());
    }

    [Fact]
    public void Set_WhenAtMaxMembers_DropsTheRightmostMember()
    {
        var incomingMembers = Enumerable.Range(0, MaxMembers)
                                        .Select(static index => $"vendor{index}=value")
                                        .ToArray();
        var state = W3CTraceState.Parse(string.Join(",", incomingMembers));

        var outgoingMembers = state.Set("mykey", "v").ToString().Split(',');

        Assert.Equal(MaxMembers, outgoingMembers.Length);
        Assert.Equal("mykey=v", outgoingMembers[0]);
        Assert.Equal(incomingMembers.Take(MaxMembers - 1), outgoingMembers.Skip(1));
    }

    [Fact]
    public void ParseAndToString_WithMoreMembersThanTheMax_KeepsAtMostTheMax()
    {
        var incomingMembers = Enumerable.Range(0, MaxMembers * 2)
                                        .Select(static index => $"vendor{index}=value")
                                        .ToArray();

        var outgoingMembers = W3CTraceState.Parse(string.Join(",", incomingMembers)).ToString().Split(',');

        Assert.Equal(MaxMembers, outgoingMembers.Length);
        Assert.Equal(incomingMembers.Take(MaxMembers), outgoingMembers);
    }

    [Fact]
    public void ParseAndToString_AtMaxMembers_RoundTripsAllMembers()
    {
        // The boundary the cap sits on: a header holding exactly the limit loses nothing.
        var tracestate = string.Join(
            ",",
            Enumerable.Range(0, MaxMembers).Select(static index => $"vendor{index}=value"));

        Assert.Equal(tracestate, W3CTraceState.Parse(tracestate).ToString());
    }

    [Theory]
    [InlineData("1abc")] // A leading digit, which only the flattened grammar allows.
    [InlineData("a@b@c")] // Several @ characters, which only the flattened grammar allows.
    [InlineData("0")]
    public void Set_WithKeyValidOnlyUnderTheFlattenedGrammar_AddsTheMember(string key)
    {
        var state = W3CTraceState.Parse("vendora=1");

        Assert.Equal($"{key}=v,vendora=1", state.Set(key, "v").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("MYKEY")]
    [InlineData("my key")]
    [InlineData("my,key")]
    [InlineData("my=key")]
    [InlineData("my.key")]
    public void Set_WithInvalidKey_KeepsTheReceiverContents(string key)
    {
        var state = W3CTraceState.Parse("vendora=1");

        var updated = state.Set(key, "v");

        Assert.Same(state, updated);
        Assert.Equal("vendora=1", updated.ToString());
    }

    [Fact]
    public void Set_WithKeyAtMaxLength_AddsTheMember()
    {
        var key = new string('a', MaxKeyLength);
        var state = W3CTraceState.Parse("vendora=1");

        Assert.Equal($"{key}=v,vendora=1", state.Set(key, "v").ToString());
    }

    [Fact]
    public void Set_WithKeyLongerThanMaxLength_KeepsTheReceiverContents()
    {
        var key = new string('a', MaxKeyLength + 1);
        var state = W3CTraceState.Parse("vendora=1");

        Assert.Equal("vendora=1", state.Set(key, "v").ToString());
    }

    [Fact]
    public void Set_WithValueContainingAnInteriorSpace_AddsTheMember()
    {
        // A value may contain spaces; only a trailing one is disallowed.
        var state = W3CTraceState.Parse("vendora=1");

        Assert.Equal("mykey=a b,vendora=1", state.Set("mykey", "a b").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("a,b")]
    [InlineData("a=b")]
    [InlineData("value ")]
    [InlineData("\tvalue")]
    public void Set_WithInvalidValue_KeepsTheReceiverContents(string value)
    {
        var state = W3CTraceState.Parse("vendora=1");

        Assert.Equal("vendora=1", state.Set("mykey", value).ToString());
    }

    [Fact]
    public void Set_WithValueAtMaxLength_AddsTheMember()
    {
        var value = new string('a', MaxValueLength);
        var state = W3CTraceState.Parse("vendora=1");

        Assert.Equal($"mykey={value},vendora=1", state.Set("mykey", value).ToString());
    }

    [Fact]
    public void Set_WithValueLongerThanMaxLength_KeepsTheReceiverContents()
    {
        var value = new string('a', MaxValueLength + 1);
        var state = W3CTraceState.Parse("vendora=1");

        Assert.Equal("vendora=1", state.Set("mykey", value).ToString());
    }

    [Fact]
    public void Set_WithNullKeyOrValue_KeepsTheReceiverContents()
    {
        // Mutating operations validate their input but never throw for it.
        var state = W3CTraceState.Parse("vendora=1");

        Assert.Equal("vendora=1", state.Set(null!, "v").ToString());
        Assert.Equal("vendora=1", state.Set("mykey", null!).ToString());
    }

    [Fact]
    public void Remove_WhenKeyExists_DeletesItAndPreservesTheOtherOrder()
    {
        var state = W3CTraceState.Parse("vendora=1,mykey=v,vendorb=2");

        Assert.Equal("vendora=1,vendorb=2", state.Remove("mykey").ToString());
    }

    [Fact]
    public void Remove_WhenKeyIsAbsent_KeepsTheReceiverContents()
    {
        var state = W3CTraceState.Parse("vendora=1,vendorb=2");

        Assert.Equal("vendora=1,vendorb=2", state.Remove("mykey").ToString());
    }

    [Fact]
    public void Remove_WhenKeyIsAbsent_ReturnsTheReceiverItself()
    {
        // Deleting a key that is not there is what a sampler does on every span, so the receiver is
        // handed straight back rather than rebuilt into an equal instance.
        var state = W3CTraceState.Parse("vendora=1,vendorb=2");

        Assert.Same(state, state.Remove("mykey"));
    }

    [Fact]
    public void Remove_DoesNotMutateTheReceiver()
    {
        var state = W3CTraceState.Parse("vendora=1,mykey=v");

        var updated = state.Remove("mykey");

        Assert.NotSame(state, updated);
        Assert.Equal("vendora=1,mykey=v", state.ToString());
    }

    [Fact]
    public void Remove_WithNullKey_KeepsTheReceiverContents()
    {
        var state = W3CTraceState.Parse("vendora=1");

        var updated = state.Remove(null!);

        Assert.Same(state, updated);
        Assert.Equal("vendora=1", updated.ToString());
    }

    [Fact]
    public void TryGetValue_WhenKeyExists_ReturnsTheValue()
    {
        var state = W3CTraceState.Parse("vendora=1, mykey=v ,vendorb=2");

        Assert.True(state.TryGetValue("mykey", out var value));
        Assert.Equal("v", value);
    }

    [Fact]
    public void TryGetValue_WhenKeyIsAbsent_ReturnsFalse()
    {
        var state = W3CTraceState.Parse("vendora=1");

        Assert.False(state.TryGetValue("mykey", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValue_WithNullKey_ReturnsFalse()
    {
        var state = W3CTraceState.Parse("vendora=1");

        Assert.False(state.TryGetValue(null!, out var value));
        Assert.Null(value);
    }
}
