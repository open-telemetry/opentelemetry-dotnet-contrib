// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// Builds a validated <see cref="LogLevelPolicy"/> from a single policy payload value.
/// </summary>
/// <remarks>
/// The accepted value is a JSON string carrying one of the severity tokens
/// <see cref="DiagnosticLogLevelParser"/> defines. It may be given directly or as the
/// <c>level</c> member of an object, whose other members are ignored.
/// </remarks>
internal sealed class LogLevelPolicyReader : PolicyReader
{
    /// <summary>
    /// The shared reader instance.
    /// </summary>
    public static readonly LogLevelPolicyReader Instance = new();

    /// <summary>
    /// The payload key that carries a diagnostic log level.
    /// </summary>
    internal const string PayloadKeyName = "log_level";

    private static readonly JsonEncodedText LevelMemberName = JsonEncodedText.Encode("level");

    private LogLevelPolicyReader()
    {
    }

    /// <inheritdoc/>
    public override string PayloadKey => PayloadKeyName;

    /// <inheritdoc/>
    public override PolicyType PolicyType => LogLevelPolicy.PolicyTypeValue;

    /// <inheritdoc/>
    public override string PolicyName => "Diagnostic log level";

    /// <inheritdoc/>
    public override PolicyReadResult Read(in JsonElement value) =>
        value.ValueKind is not JsonValueKind.Object
            ? this.ReadLevel(value, "The log level must be a string or an object.")
            : JsonValueReader.TryGetSingleMember(value, LevelMemberName.EncodedUtf8Bytes, out var member) switch
            {
                JsonMemberLookup.Found => this.ReadLevel(member, "The 'level' member must be a string."),

                JsonMemberLookup.Missing => PolicyReadResult.Reject(
                    PolicyRejectionReason.InvalidPayloadShape,
                    "The log level object does not declare a 'level' member."),

                JsonMemberLookup.Repeated => PolicyReadResult.Reject(
                    PolicyRejectionReason.InvalidPayloadShape,
                    "The log level object declares 'level' more than once."),

                var lookup => throw new InvalidOperationException($"Unhandled {nameof(JsonMemberLookup)}: {lookup}"),
            };

    private PolicyReadResult ReadLevel(in JsonElement value, string shapeError) =>
        value.ValueKind is JsonValueKind.String
            ? this.ReadLevelValue(value)
            : PolicyReadResult.Reject(PolicyRejectionReason.InvalidPayloadShape, shapeError);

    private PolicyReadResult ReadLevelValue(in JsonElement value) =>
        JsonValueReader.TryGetText(value, out var text)
            && DiagnosticLogLevelParser.TryParse(text, out var level)
            ? this.CreatePolicy(level)
            : PolicyReadResult.Reject(
                PolicyRejectionReason.InvalidPolicyValue,
                $"The log level must be one of {DiagnosticLogLevelParser.AcceptedTokens}.");

    private PolicyReadResult CreatePolicy(DiagnosticLogLevel level) =>
        LogLevelPolicy.TryCreate(this.PolicyId, this.PolicyName, level, out var policy, out var createError)
            ? PolicyReadResult.Success(policy)
            : PolicyReadResult.Reject(PolicyRejectionReason.InvalidPolicyValue, createError);
}
