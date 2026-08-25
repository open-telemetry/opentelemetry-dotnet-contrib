// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// Provides non-throwing helpers for reading policy values from JSON.
/// </summary>
internal static class JsonValueReader
{
    /// <summary>
    /// Attempts to read a value as text.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <param name="text">
    /// When this method returns <see langword="true"/>, the text the value carries;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the value is a JSON string with a text equivalent;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Returns <see langword="false"/> if <see cref="JsonElement.GetString"/> cannot
    /// materialize the value, including an escaped unpaired surrogate.
    /// </remarks>
    public static bool TryGetText(in JsonElement value, [NotNullWhen(true)] out string? text)
    {
        if (value.ValueKind is not JsonValueKind.String)
        {
            text = null;
            return false;
        }

        try
        {
            text = value.GetString();
            return text is not null;
        }
        catch (InvalidOperationException)
        {
            text = null;
            return false;
        }
    }

    /// <summary>
    /// Looks for a single member of an object by name.
    /// </summary>
    /// <param name="value">The object to search. Must be of kind <see cref="JsonValueKind.Object"/>.</param>
    /// <param name="utf8MemberName">The UTF-8 encoded member name to look for.</param>
    /// <param name="member">
    /// When this method returns <see cref="JsonMemberLookup.Found"/>, the value the member
    /// carries; otherwise the default element.
    /// </param>
    /// <returns>The outcome of the lookup.</returns>
    /// <remarks>
    /// Returns <see cref="JsonMemberLookup.Repeated"/> rather than selecting an occurrence
    /// when the member appears more than once.
    /// </remarks>
    public static JsonMemberLookup TryGetSingleMember(
        in JsonElement value,
        ReadOnlySpan<byte> utf8MemberName,
        out JsonElement member)
    {
        member = default;
        var found = false;

        foreach (var candidate in value.EnumerateObject())
        {
            if (!candidate.NameEquals(utf8MemberName))
            {
                continue;
            }

            if (found)
            {
                member = default;
                return JsonMemberLookup.Repeated;
            }

            member = candidate.Value;
            found = true;
        }

        return found ? JsonMemberLookup.Found : JsonMemberLookup.Missing;
    }
}
