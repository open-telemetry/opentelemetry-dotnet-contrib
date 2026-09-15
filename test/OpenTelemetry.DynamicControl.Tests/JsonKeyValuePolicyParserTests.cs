// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Tests;

public class JsonKeyValuePolicyParserTests
{
    private const int MaxSupportedDepth = 64;

    private static readonly PayloadEntryLocation SamplingRateLocation = PayloadEntryLocation.ForKey("sampling_rate");

    public static TheoryData<string> MalformedPayloads =>
    [
        string.Empty,
        "   ",
        "{",
        "{\"sampling_rate\": }",
        "5",
        "\"x\"",
        "true",
        "null",
        "{\"sampling_rate\": 0.5,}",
        "{/*c*/\"sampling_rate\": 0.5}",
        CreateNestedArrays(MaxSupportedDepth + 1),
    ];

    public static TheoryData<string, double> AcceptedPayloads =>
        new()
        {
            { "{\"sampling_rate\": 0.5}", 0.5 },
            { "{\"sampling_rate\": \"0.5\"}", 0.5 },
            { "{\"sampling_rate\": \" 0.5 \"}", 0.5 },
            { "{\"sampling_rate\": \"5e-1\"}", 0.5 },
            { "{\"sampling_rate\": {\"probability\": 0.5}}", 0.5 },
            { "{\"sampling_rate\": {\"probability\": \"0.5\"}}", 0.5 },
            { "{\"sampling_rate\": {\"probability\": 0.5, \"future\": 1}}", 0.5 },
            { "{\"sampling_rate\": 0}", 0 },
            { "{\"sampling_rate\": -0.0}", 0 },
            { "{\"sampling_rate\": 1}", 1 },
            { "[{\"sampling_rate\": 0.5}]", 0.5 },
        };

    [Theory]
    [MemberData(nameof(MalformedPayloads))]
    public void Parse_WithUndecodablePayload_ReportsMalformed(string payload) =>
        AssertMalformed(ParseUtf8(payload));

    [Theory]
    [MemberData(nameof(AcceptedPayloads))]
    public void Parse_WithUsableEntry_ReturnsOnePolicy(string payload, double expectedProbability)
    {
        var result = ParseUtf8(payload);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Rejections);

        var policy = Assert.IsType<TraceSamplingRatePolicy>(Assert.Single(result.Policies));
        Assert.Equal(expectedProbability, policy.SamplingProbability);
        Assert.Equal(TraceSamplingRatePolicy.PolicyTypeValue.Value, policy.Id.Value);
        Assert.Equal("Trace sampling rate", policy.Name);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Parse_WithNoEntries_DecodesAnEmptySet(string payload)
    {
        var result = ParseUtf8(payload);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Null(result.Error);
        Assert.Empty(result.Policies);
        Assert.Empty(result.Rejections);
        Assert.Empty(result.IgnoredKeys);
    }

    [Theory]
    [InlineData("{\"sampling_rate\": true}")]
    [InlineData("{\"sampling_rate\": null}")]
    [InlineData("{\"sampling_rate\": []}")]
    [InlineData("{\"sampling_rate\": {}}")]
    [InlineData("{\"sampling_rate\": {\"probability\": true}}")]
    [InlineData("{\"sampling_rate\": {\"probability\": 0.1, \"probability\": 0.2}}")]
    public void Parse_WithUnusableEntryShape_RejectsTheEntry(string payload) =>
        AssertSingleRejection(payload, SamplingRateLocation, PolicyRejectionReason.SchemaMismatch);

    [Theory]
    [InlineData("{\"sampling_rate\": 1.5}")]
    [InlineData("{\"sampling_rate\": -0.1}")]
    [InlineData("{\"sampling_rate\": 1e400}")]
    [InlineData("{\"sampling_rate\": 1e-4000}")]
    [InlineData("{\"sampling_rate\": \"abc\"}")]
    [InlineData("{\"sampling_rate\": \"\"}")]
    [InlineData("{\"sampling_rate\": \"NaN\"}")]
    [InlineData("{\"sampling_rate\": \"Infinity\"}")]
    [InlineData("{\"sampling_rate\": \"1e-4000\"}")]
    public void Parse_WithUnusableEntryValue_RejectsTheEntry(string payload) =>
        AssertSingleRejection(payload, SamplingRateLocation, PolicyRejectionReason.InvalidValue);

    [Fact]
    public void Parse_WithUnrecognizedKey_IgnoresIt()
    {
        var result = ParseUtf8("{\"other\": 0.5}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Policies);
        Assert.Empty(result.Rejections);
        Assert.Equal(["other"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_WithRecognizedAndUnrecognizedKeys_CommitsTheRecognizedOne()
    {
        var result = ParseUtf8("{\"sampling_rate\": 0.5, \"send_logs\": true}");

        Assert.Single(result.Policies);
        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Rejections);
        Assert.Equal(["send_logs"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_MatchesKeysOrdinally()
    {
        var result = ParseUtf8("{\"SAMPLING_RATE\": 0.5}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Policies);
        Assert.Empty(result.Rejections);
        Assert.Equal(["SAMPLING_RATE"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_ReportsEachIgnoredKeyOnce()
    {
        var result = ParseUtf8("{\"other\": 1, \"other\": 2}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Equal(["other"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_ReportsIgnoredKeysInDocumentOrder()
    {
        var result = ParseUtf8("{\"zebra\": 1, \"apple\": 2}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Equal(["zebra", "apple"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Theory]
    [InlineData("{\"sampling_rate\": 0.1, \"sampling_rate\": 0.2}")]
    [InlineData("[{\"sampling_rate\": 0.1}, {\"sampling_rate\": 0.2}]")]
    [InlineData("{\"sampling_rate\": 1.5, \"sampling_rate\": 0.5}")]
    public void Parse_WithRepeatedRecognizedKey_RejectsEveryOccurrence(string payload)
    {
        var result = ParseUtf8(payload);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Policies);
        Assert.Equal(2, result.Rejections.Length);
        Assert.All(result.Rejections, rejection =>
        {
            Assert.Equal(SamplingRateLocation, rejection.Location);
            Assert.Equal(PolicyRejectionReason.DuplicateKey, rejection.Reason);
        });
    }

    [Fact]
    public void Parse_WithRepeatedRecognizedKey_DoesNotAffectOtherEntries()
    {
        var result = ParseUtf8(
            "{\"sampling_rate\": 0.1, \"other\": 1, \"sampling_rate\": 0.2}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Policies);
        Assert.Equal(2, result.Rejections.Length);
        Assert.Equal(["other"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Theory]
    [InlineData("[0.5]")]
    [InlineData("[[]]")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    [InlineData("[{\"sampling_rate\": 0.5, \"x\": 1}]")]
    public void Parse_WithArrayElementThatIsNotASingleEntry_RejectsTheElement(string payload)
    {
        var result = ParseUtf8(payload);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Policies);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(PayloadEntryLocation.ForIndex(0), rejection.Location);
        Assert.Equal(PolicyRejectionReason.SchemaMismatch, rejection.Reason);
    }

    [Fact]
    public void Parse_WithArrayElementCarryingAnUnrecognizedKey_IgnoresIt()
    {
        var result = ParseUtf8("[{\"a\": 1}, {\"sampling_rate\": 0.5}]");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Single(result.Policies);
        Assert.Empty(result.Rejections);
        Assert.Equal(["a"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_WithAnUnusableSiblingEntry_CommitsTheUsableOne()
    {
        var result = ParseUtf8("[{\"a\": true}, {\"sampling_rate\": 0.5}, [1]]");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Single(result.Policies);
        Assert.Single(result.Rejections);
        Assert.Equal(["a"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_IsDeterministicForTheSamePayload()
    {
        const string Payload = "{\"sampling_rate\": 0.25, \"other\": 1}";

        var first = ParseUtf8(Payload);
        var second = ParseUtf8(Payload);

        Assert.False(first.IsMalformed, "The first parse result should not be malformed.");
        Assert.False(second.IsMalformed, "The second parse result should not be malformed.");
        Assert.Equal(
            ((TraceSamplingRatePolicy)first.Policies[0]).SamplingProbability,
            ((TraceSamplingRatePolicy)second.Policies[0]).SamplingProbability);
        Assert.Equal((IEnumerable<string>)first.IgnoredKeys, second.IgnoredKeys);
    }

    [Fact]
    public void Parse_WithCommaDecimalCurrentCulture_ParsesStringValuesInvariantly()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var result = ParseUtf8("{\"sampling_rate\": \"0.5\"}");

            var policy = Assert.IsType<TraceSamplingRatePolicy>(Assert.Single(result.Policies));
            Assert.Equal(0.5, policy.SamplingProbability);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Parse_ReportsRejectionsInDocumentOrder()
    {
        var result = ParseUtf8("[{\"sampling_rate\": true}, [1]]");

        Assert.Equal(2, result.Rejections.Length);
        Assert.Equal(SamplingRateLocation, result.Rejections[0].Location);
        Assert.Equal(PayloadEntryLocation.ForIndex(1), result.Rejections[1].Location);
    }

    [Fact]
    public void Parse_WithInvalidUtf8InAValue_RejectsTheEntry()
    {
        // No dedicated well-formedness check runs ahead of System.Text.Json: invalid UTF-8
        // inside a string value surfaces as InvalidOperationException from GetString, which
        // is handled the same way as any other unreadable string (for example an escaped
        // unpaired surrogate), rejecting only the entry that carries it.
        var payload = CreatePayloadWithInvalidUtf8("{\"sampling_rate\": \"", "\"}");

        var result = JsonKeyValuePolicyParser.Parse(payload);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Null(result.Error);
        Assert.Empty(result.Policies);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(SamplingRateLocation, rejection.Location);
        Assert.Equal(PolicyRejectionReason.InvalidValue, rejection.Reason);
    }

    [Fact]
    public void Parse_WithInvalidUtf8InAKey_RejectsTheEntryWithoutALocation()
    {
        var payload = CreatePayloadWithInvalidUtf8("{\"", "\": 1, \"sampling_rate\": 0.5}");

        var result = JsonKeyValuePolicyParser.Parse(payload);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Null(result.Error);
        Assert.Single(result.Policies);
        Assert.Empty(result.IgnoredKeys);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(default, rejection.Location);
        Assert.Equal(PolicyRejectionReason.SchemaMismatch, rejection.Reason);
    }

    [Fact]
    public void Parse_WithInvalidUtf8OutsideAnyString_ReportsMalformed()
    {
        // Bytes that break JSON syntax outright, rather than sitting inside a string value,
        // are still caught: they are never valid JSON tokens, so JsonDocument.Parse itself
        // rejects them regardless of encoding.
        var payload = CreatePayloadWithInvalidUtf8("{\"sampling_rate\": 0.5}", string.Empty);

        var result = JsonKeyValuePolicyParser.Parse(payload);

        AssertMalformed(result);
    }

    [Fact]
    public void Parse_WithInvalidUtf8InOneEntry_StillCommitsSiblingEntries()
    {
        var payload = CreatePayloadWithInvalidUtf8("{\"sampling_rate\": 0.5, \"log_level\": \"", "\"}");

        var result = JsonKeyValuePolicyParser.Parse(payload);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Single(result.Policies);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(PayloadEntryLocation.ForKey("log_level"), rejection.Location);
        Assert.Equal(PolicyRejectionReason.InvalidValue, rejection.Reason);
    }

    [Fact]
    public void Parse_WithMultiByteAndAstralContent_DecodesThePayload()
    {
        // Two- and four-byte sequences are well-formed UTF-8 and must decode as ordinary
        // ignored keys, not be mistaken for unreadable ones.
        var result = ParseUtf8("{\"caf\u00e9\": 1, \"\U0001F600\": 2, \"sampling_rate\": 0.5}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Single(result.Policies);
        Assert.Equal(["caf\u00e9", "\U0001F600"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_WithByteOrderMark_DecodesThePayload()
    {
        byte[] payload = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("{\"sampling_rate\": 0.5}")];

        var result = JsonKeyValuePolicyParser.Parse(payload);

        var policy = Assert.IsType<TraceSamplingRatePolicy>(Assert.Single(result.Policies));
        Assert.Equal(0.5, policy.SamplingProbability);
    }

    [Fact]
    public void Parse_WithByteOrderMarkAlone_ReportsMalformed()
    {
        var result = JsonKeyValuePolicyParser.Parse(Encoding.UTF8.GetPreamble());

        Assert.True(result.IsMalformed, "The result should be identified as malformed.");
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_WithByteOrderMarkAfterTheFirstByte_ReportsMalformed()
    {
        byte[] payload = [.. Encoding.UTF8.GetBytes(" "), .. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("{}")];

        var result = JsonKeyValuePolicyParser.Parse(payload);

        Assert.True(result.IsMalformed, "The result should be identified as malformed.");
    }

    [Fact]
    public void Parse_WithMultiByteUnrecognizedKey_IgnoresIt()
    {
        var result = ParseUtf8("{\"caf\u00e9\": 1, \"sampling_rate\": 0.5}");

        Assert.Single(result.Policies);
        Assert.Equal(["caf\u00e9"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_WithUnreadableEscapedValue_RejectsTheEntry()
    {
        AssertSingleRejection(
            "{\"sampling_rate\": \"\\uD800\"}",
            SamplingRateLocation,
            PolicyRejectionReason.InvalidValue);
    }

    [Fact]
    public void Parse_WithUnreadableEscapedKey_RejectsTheEntryWithoutALocation()
    {
        var result = ParseUtf8("{\"sampling_rat\\uD800\": 1, \"sampling_rate\": 0.5}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Single(result.Policies);
        Assert.Empty(result.IgnoredKeys);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(default, rejection.Location);
        Assert.Equal(PolicyRejectionReason.SchemaMismatch, rejection.Reason);
    }

    [Fact]
    public void Parse_WithOnlyAnUnreadableEscapedKey_DecodesAnEmptySet()
    {
        var result = ParseUtf8("{\"sampling_rat\\uD800\": 1}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Policies);
        Assert.Empty(result.IgnoredKeys);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(default, rejection.Location);
        Assert.Equal(PolicyRejectionReason.SchemaMismatch, rejection.Reason);
    }

    [Fact]
    public void Parse_WithUnreadableEscapedKeyInAnArray_ReportsThePosition()
    {
        var result = ParseUtf8("[{\"sampling_rate\": 0.5}, {\"sampling_rat\\uD800\": 1}]");

        Assert.Single(result.Policies);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(PayloadEntryLocation.ForIndex(1), rejection.Location);
        Assert.Equal(PolicyRejectionReason.SchemaMismatch, rejection.Reason);
    }

    [Fact]
    public void Parse_WithUnreadableEscapedMemberName_RejectsTheEntry() =>
        AssertSingleRejection(
            "{\"sampling_rate\": {\"probabilit\\uD800\": 0.5}}",
            SamplingRateLocation,
            PolicyRejectionReason.SchemaMismatch);

    [Fact]
    public void Parse_WithUnreadableEscapedSiblingMemberName_IgnoresIt()
    {
        var result = ParseUtf8(
            "{\"sampling_rate\": {\"probabilit\\uD800\": 1, \"probability\": 0.5}}");

        var policy = Assert.IsType<TraceSamplingRatePolicy>(Assert.Single(result.Policies));
        Assert.Equal(0.5, policy.SamplingProbability);
    }

    [Fact]
    public void Parse_MatchesKeysAfterUnescaping()
    {
        var result = ParseUtf8("{\"sampling_r\\u0061te\": 0.5}");

        var policy = Assert.IsType<TraceSamplingRatePolicy>(Assert.Single(result.Policies));
        Assert.Equal(0.5, policy.SamplingProbability);
    }

    [Fact]
    public void Parse_MatchesTheProbabilityMemberAfterUnescaping()
    {
        var result = ParseUtf8("{\"sampling_rate\": {\"probabilit\\u0079\": 0.5}}");

        var policy = Assert.IsType<TraceSamplingRatePolicy>(Assert.Single(result.Policies));
        Assert.Equal(0.5, policy.SamplingProbability);
    }

    [Fact]
    public void Parse_WithDeeplyNestedUnrecognizedKey_StillCommitsRecognizedEntries()
    {
        var payload = "{\"other\": "
            + CreateNestedArrays(MaxSupportedDepth - 2)
            + ", \"sampling_rate\": 0.5}";

        var result = ParseUtf8(payload);

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Single(result.Policies);
        Assert.Equal(["other"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_WithNestingAtTheDepthLimit_DecodesThePayload()
    {
        // The boundary itself, so that the cap is asserted rather than merely exceeded. The
        // cap applies to the whole payload, which is why it is set at the System.Text.Json
        // default: a tighter one would let a branch nested by another SDK discard the
        // entries this package can read.
        var result = ParseUtf8("{\"other\": " + CreateNestedArrays(MaxSupportedDepth - 1) + "}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Equal(["other"], (IEnumerable<string>)result.IgnoredKeys);
    }

    [Fact]
    public void Parse_WithEveryRecognizedKey_CommitsOnePolicyEach()
    {
        var result = ParseUtf8("{\"sampling_rate\": 0.5, \"log_level\": \"warn\"}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Rejections);
        Assert.Empty(result.IgnoredKeys);

        Assert.Collection(
            result.Policies,
            policy => Assert.Equal(0.5, Assert.IsType<TraceSamplingRatePolicy>(policy).SamplingProbability),
            policy => Assert.Equal(LogLevelPolicy.PolicyTypeValue, policy.PolicyType));
    }

    [Fact]
    public void Parse_RoutesEachKeyToItsOwnReader()
    {
        // The keys carry values the other reader would refuse, so a policy of each type can
        // only appear if the key selected the reader rather than the value's shape doing so.
        var result = ParseUtf8("{\"log_level\": \"trace\", \"sampling_rate\": \"0.25\"}");

        Assert.False(result.IsMalformed, "The result should not be identified as malformed.");
        Assert.Empty(result.Rejections);
        Assert.Equal(2, result.Policies.Length);
        Assert.Equal(LogLevelPolicy.PolicyTypeValue, result.Policies[0].PolicyType);
        Assert.Equal(0.25, Assert.IsType<TraceSamplingRatePolicy>(result.Policies[1]).SamplingProbability);
    }

    [Fact]
    public void Parse_WithOneUnusableRecognizedKey_CommitsTheOther()
    {
        var result = ParseUtf8("{\"sampling_rate\": 0.5, \"log_level\": \"nonsense\"}");

        Assert.Single(result.Policies);
        Assert.Empty(result.IgnoredKeys);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(PayloadEntryLocation.ForKey("log_level"), rejection.Location);
        Assert.Equal(PolicyRejectionReason.InvalidValue, rejection.Reason);
    }

    [Fact]
    public void Parse_WithRepeatedLogLevelKey_RejectsEveryOccurrence()
    {
        var result = ParseUtf8("{\"log_level\": \"warn\", \"log_level\": \"error\"}");

        Assert.Empty(result.Policies);
        Assert.Equal(2, result.Rejections.Length);
        Assert.All(result.Rejections, rejection =>
        {
            Assert.Equal(PayloadEntryLocation.ForKey("log_level"), rejection.Location);
            Assert.Equal(PolicyRejectionReason.DuplicateKey, rejection.Reason);
        });
    }

    [Fact]
    public void Parse_MatchesTheLogLevelKeyOrdinally()
    {
        var result = ParseUtf8("{\"LOG_LEVEL\": \"warn\"}");

        Assert.Empty(result.Policies);
        Assert.Empty(result.Rejections);
        Assert.Equal(["LOG_LEVEL"], (IEnumerable<string>)result.IgnoredKeys);
    }

    private static string CreateNestedArrays(int depth)
        => new string('[', depth) + "0.5" + new string(']', depth);

    // The parser reads UTF-8 because that is the form every transport delivers a payload in.
    // Tests state their payloads as source text and encode them here, so that each case
    // remains readable and the encoding step is not repeated in every one.
    private static PolicyPayloadParseResult ParseUtf8(string payload) =>
        JsonKeyValuePolicyParser.Parse(Encoding.UTF8.GetBytes(payload));

    // Encoding a string cannot produce invalid UTF-8, so the bytes are assembled directly.
    // 0xC3 opens a two-byte sequence and 0x28 is not a continuation byte, which System.Text.Json
    // accepts while parsing and only fails on when the text it encodes is read.
    private static ReadOnlyMemory<byte> CreatePayloadWithInvalidUtf8(string before, string after)
    {
        byte[] payload = [.. Encoding.UTF8.GetBytes(before), 0xC3, 0x28, .. Encoding.UTF8.GetBytes(after)];
        return payload;
    }

    private static void AssertMalformed(PolicyPayloadParseResult result)
    {
        Assert.True(result.IsMalformed, "The result should be identified as malformed.");
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
        Assert.Empty(result.Policies);
        Assert.Empty(result.Rejections);
        Assert.Empty(result.IgnoredKeys);
    }

    private static void AssertSingleRejection(string payload, PayloadEntryLocation expectedLocation, PolicyRejectionReason expectedReason)
    {
        var result = ParseUtf8(payload);

        Assert.False(result.IsMalformed);
        Assert.Null(result.Error);
        Assert.Empty(result.Policies);
        Assert.Empty(result.IgnoredKeys);

        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(expectedLocation, rejection.Location);
        Assert.Equal(expectedReason, rejection.Reason);
        Assert.NotEmpty(rejection.Message);
    }
}
