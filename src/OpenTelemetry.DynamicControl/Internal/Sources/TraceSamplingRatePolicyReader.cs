// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// Builds a validated <see cref="TraceSamplingRatePolicy"/> from a single policy payload
/// value.
/// </summary>
/// <remarks>
/// The accepted value is a probability given as a JSON number or as a numeric JSON string,
/// either directly or as the <c>probability</c> member of an object. Unrecognized members of
/// that object are ignored, so a payload describing a richer target than this package
/// understands still yields a policy.
/// </remarks>
internal sealed class TraceSamplingRatePolicyReader : PolicyReader
{
    /// <summary>
    /// The shared reader instance.
    /// </summary>
    public static readonly TraceSamplingRatePolicyReader Instance = new();

    /// <summary>
    /// The payload key that carries a trace sampling rate.
    /// </summary>
    internal const string PayloadKeyName = "sampling_rate";

    private static readonly JsonEncodedText ProbabilityMemberName = JsonEncodedText.Encode("probability");

    private TraceSamplingRatePolicyReader()
    {
    }

    /// <inheritdoc/>
    public override string PayloadKey => PayloadKeyName;

    /// <inheritdoc/>
    public override PolicyType PolicyType => TraceSamplingRatePolicy.PolicyTypeValue;

    /// <inheritdoc/>
    public override string PolicyName => "Trace sampling rate";

    /// <inheritdoc/>
    public override PolicyReadResult Read(in JsonElement value) =>
        value.ValueKind is not JsonValueKind.Object
            ? this.ReadProbability(value, "The sampling rate must be a number, a numeric string, or an object.")
            : JsonValueReader.TryGetSingleMember(value, ProbabilityMemberName.EncodedUtf8Bytes, out var member) switch
            {
                JsonMemberLookup.Found => this.ReadProbability(
                    member,
                    "The 'probability' member must be a number or a numeric string."),

                JsonMemberLookup.Missing => PolicyReadResult.Reject(
                    PolicyRejectionReason.SchemaMismatch,
                    "The sampling rate object does not declare a 'probability' member."),

                JsonMemberLookup.Repeated => PolicyReadResult.Reject(
                    PolicyRejectionReason.SchemaMismatch,
                    "The sampling rate object declares 'probability' more than once."),

                JsonMemberLookup.Unspecified => throw JsonValueReader.UnhandledLookup(JsonMemberLookup.Unspecified),
                var lookup => throw JsonValueReader.UnhandledLookup(lookup),
            };

    private static bool TryGetProbability(in JsonElement value, out double probability)
    {
        if (value.ValueKind is JsonValueKind.Number)
        {
            if (!value.TryGetDouble(out probability)
                || double.IsNaN(probability)
                || double.IsInfinity(probability)
                || probability < 0)
            {
                probability = default;
                return false;
            }

            return probability != 0
                || TraceSamplingRateParser.TryParse(value.GetRawText(), out probability);
        }

        if (JsonValueReader.TryGetText(value, out var text))
        {
            return TraceSamplingRateParser.TryParse(text, out probability);
        }

        probability = default;
        return false;
    }

    private PolicyReadResult ReadProbability(in JsonElement value, string shapeError) =>
        value.ValueKind is JsonValueKind.Number or JsonValueKind.String
            ? this.ReadProbabilityValue(value)
            : PolicyReadResult.Reject(PolicyRejectionReason.SchemaMismatch, shapeError);

    private PolicyReadResult ReadProbabilityValue(in JsonElement value) =>
        TryGetProbability(value, out var probability)
            ? this.CreatePolicy(probability)
            : PolicyReadResult.Reject(
                PolicyRejectionReason.InvalidValue,
                "The sampling rate must be a supported non-negative number.");

    private PolicyReadResult CreatePolicy(double probability) =>
        TraceSamplingRatePolicy.TryCreate(this.PolicyId, this.PolicyName, probability, out var policy, out var createError)
            ? PolicyReadResult.Success(policy)
            : PolicyReadResult.Reject(PolicyRejectionReason.InvalidValue, createError);
}
