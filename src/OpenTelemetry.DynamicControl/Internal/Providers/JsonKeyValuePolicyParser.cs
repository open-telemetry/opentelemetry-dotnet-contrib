// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using OpenTelemetry.DynamicControl.Internal.Policies;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Decodes a complete policy payload in which each entry is a key naming a policy type and
/// a value describing that policy.
/// </summary>
/// <remarks>
/// <para>
/// The payload root is either an object, whose properties are the entries, or an array,
/// whose elements are each an object carrying exactly one entry. Keys this package does not
/// recognize are ignored, so a payload shared with other SDKs is usable.
/// </para>
/// <para>
/// Decoding is deterministic: the same payload always yields the same policies, in the order
/// the payload declared them, and nothing outside the payload participates.
/// </para>
/// </remarks>
internal static class JsonKeyValuePolicyParser
{
    // The set of policy types this package can decode. A reader is stateless and shared, so
    // the table is built once. It is ordered only for determinism of key matching; no reader
    // takes precedence over another, because each claims a distinct payload key.
    private static readonly ImmutableArray<PolicyReader> Readers =
    [
        TraceSamplingRatePolicyReader.Instance,
        LogLevelPolicyReader.Instance,
    ];

    // The depth cap is stated rather than inherited so that raising it is a deliberate act.
    // It is set at the System.Text.Json default.
    private static readonly JsonDocumentOptions PayloadOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 64,
    };

    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Decodes one complete policy payload.
    /// </summary>
    /// <param name="utf8Payload">
    /// The payload, encoded as UTF-8. A leading byte order mark is permitted. A payload that
    /// is not well-formed UTF-8 is malformed as a whole.
    /// </param>
    /// <returns>
    /// A result that is either malformed, meaning the payload could not be decoded and says
    /// nothing about the policies its source intends, or decoded, carrying the complete set
    /// of policies the payload declared. That set may be empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// No payload content causes this method to throw. Content that cannot be decoded is
    /// reported through the returned result.
    /// </para>
    /// <para>
    /// The payload is read in place rather than copied, so the caller must not modify it for
    /// the duration of the call.
    /// </para>
    /// </remarks>
    public static PolicyPayloadParseResult Parse(ReadOnlyMemory<byte> utf8Payload)
    {
        var payload = TrimByteOrderMark(utf8Payload);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(payload, PayloadOptions);
        }
        catch (JsonException ex)
        {
            return PolicyPayloadParseResult.Malformed("The payload could not be decoded as JSON. " + ex.Message);
        }

        // Every policy is built before the document is disposed, so nothing that outlives
        // this scope holds a JsonElement into the document's pooled buffers.
        using (document)
        {
            var root = document.RootElement;

            return root.ValueKind switch
            {
                JsonValueKind.Array => BuildResult(CollectArrayEntries(root)),
                JsonValueKind.Object => BuildResult(CollectObjectEntries(root)),
                _ => PolicyPayloadParseResult.Malformed("The payload root must be a JSON object or array."),
            };
        }
    }

    private static ReadOnlyMemory<byte> TrimByteOrderMark(ReadOnlyMemory<byte> payload)
        => payload.Span.StartsWith(Utf8ByteOrderMark)
            ? payload.Slice(Utf8ByteOrderMark.Length)
            : payload;

    private static List<PayloadEntry> CollectObjectEntries(in JsonElement root)
    {
        List<PayloadEntry> entries = [];

        foreach (var property in root.EnumerateObject())
        {
            entries.Add(CreateEntry(property, default));
        }

        return entries;
    }

    private static List<PayloadEntry> CollectArrayEntries(in JsonElement root)
    {
        List<PayloadEntry> entries = [];
        var index = 0;

        foreach (var element in root.EnumerateArray())
        {
            var location = PayloadEntryLocation.ForIndex(index);

            entries.Add(
                element.ValueKind is JsonValueKind.Object && TryGetSingleProperty(element, out var property)
                    ? CreateEntry(property, location)
                    : new PayloadEntry(new PolicyPayloadRejection(
                        location,
                        PolicyRejectionReason.SchemaMismatch,
                        "The payload array element is not an object with exactly one property.")));

            index++;
        }

        return entries;
    }

    private static PayloadEntry CreateEntry(in JsonProperty property, PayloadEntryLocation fallbackLocation)
        => TryGetKey(property, out var key)
            ? new(key, property.Value)
            : new(new PolicyPayloadRejection(
                fallbackLocation,
                PolicyRejectionReason.SchemaMismatch,
                "The payload declares a key that cannot be read as text."));

    private static bool TryGetKey(in JsonProperty property, [NotNullWhen(true)] out string? key)
    {
        // NameEquals compares against the encoded name, which avoids an allocation for the
        // common case. A name with no string equivalent cannot match a readable payload key.
        try
        {
            foreach (var reader in Readers)
            {
                if (property.NameEquals(reader.PayloadKey))
                {
                    key = reader.PayloadKey;
                    return true;
                }
            }

            key = property.Name;
            return true;
        }
        catch (InvalidOperationException)
        {
            // The name has no string equivalent. JSON permits escaped unpaired surrogates
            // (e.g. \uD800), which no UTF-16 string can represent.
            key = null;
            return false;
        }
    }

    private static bool TryGetSingleProperty(in JsonElement element, out JsonProperty property)
    {
        property = default;
        var found = false;

        foreach (var candidate in element.EnumerateObject())
        {
            if (found)
            {
                property = default;
                return false;
            }

            property = candidate;
            found = true;
        }

        return found;
    }

    private static PolicyPayloadParseResult BuildResult(List<PayloadEntry> entries)
    {
        var duplicateKeys = FindDuplicateKeys(entries);

        List<TelemetryPolicy> policies = [];
        List<PolicyPayloadRejection> rejections = [];
        List<string> ignoredKeys = [];
        HashSet<string> seenIgnoredKeys = new(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.Rejection is { } rejection)
            {
                rejections.Add(rejection);
                continue;
            }

            var key = entry.Key!;

            if (!TryGetReader(key, out var reader))
            {
                if (seenIgnoredKeys.Add(key))
                {
                    ignoredKeys.Add(key);
                }

                continue;
            }

            if (duplicateKeys?.Contains(key) == true)
            {
                rejections.Add(new(
                    PayloadEntryLocation.ForKey(key),
                    PolicyRejectionReason.DuplicateKey,
                    "The payload declares the key more than once. Every occurrence is excluded."));
                continue;
            }

            var result = reader.Read(entry.Value);

            if (result.TryGetPolicy(out var policy))
            {
                policies.Add(policy);
            }
            else
            {
                rejections.Add(result.ToRejection(PayloadEntryLocation.ForKey(key)));
            }
        }

        return PolicyPayloadParseResult.Decoded(
            [.. policies],
            [.. rejections],
            [.. ignoredKeys]);
    }

    // A recognized key maps to exactly one policy type, and identity is derived from that
    // type, so a key repeated anywhere in the payload would otherwise yield two policies
    // occupying one PolicyKey. Rejecting every occurrence is what prevents that; resolving
    // to the last would make the committed set depend on the order the payload was written.
    private static HashSet<string>? FindDuplicateKeys(List<PayloadEntry> entries)
    {
        HashSet<string>? seen = null;
        HashSet<string>? duplicates = null;

        foreach (var entry in entries)
        {
            if (entry.Key is not { } key || !TryGetReader(key, out _))
            {
                continue;
            }

            seen ??= new(StringComparer.Ordinal);

            if (!seen.Add(key))
            {
                duplicates ??= new(StringComparer.Ordinal);
                duplicates.Add(key);
            }
        }

        return duplicates;
    }

    private static bool TryGetReader(string key, [NotNullWhen(true)] out PolicyReader? reader)
    {
        foreach (var candidate in Readers)
        {
            if (string.Equals(key, candidate.PayloadKey, StringComparison.Ordinal))
            {
                reader = candidate;
                return true;
            }
        }

        reader = null;
        return false;
    }

    private readonly struct PayloadEntry
    {
        public PayloadEntry(string key, JsonElement value)
        {
            this.Key = key;
            this.Value = value;
            this.Rejection = null;
        }

        public PayloadEntry(PolicyPayloadRejection rejection)
        {
            this.Key = null;
            this.Value = default;
            this.Rejection = rejection;
        }

        public string? Key { get; }

        public JsonElement Value { get; }

        public PolicyPayloadRejection? Rejection { get; }
    }
}
