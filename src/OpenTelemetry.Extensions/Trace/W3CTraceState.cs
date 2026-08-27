// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Trace;

/// <summary>
/// Holds a W3C
/// <see href="https://www.w3.org/TR/2021/REC-trace-context-1-20211123/#tracestate-header">
/// <c>tracestate</c></see> header in parsed form, so that a sampler or a propagator can read and
/// change one entry without hand-writing the syntax rules that come with the header. Those are the
/// get, add, update and delete operations the OpenTelemetry
/// <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/v1.60.0/specification/trace/api.md#tracestate">
/// Tracing API</see> specification defines for <c>TraceState</c>.
/// </summary>
/// <remarks>
/// Members this instance did not write are handed back exactly as they arrived, malformed ones
/// included, so that a long chain of edits never erodes another vendor's entries.
/// <para/>
/// Nothing here mutates and nothing here throws: an edit that changes something returns a new
/// instance, while an operation that changes nothing, such as one naming an invalid key or value,
/// hands back the receiver itself rather than a copy.
/// <para/>
/// At most 32 members are kept, which is all the header grammar allows; anything past that is
/// dropped from the right as it arrives.
/// </remarks>
public sealed class W3CTraceState
{
    /// <summary>
    /// The maximum number of members in a W3C <c>tracestate</c> value, and so the most this type
    /// ever retains.
    /// </summary>
    private const int MaxMembers = 32;

    /// <summary>
    /// The maximum length of a key, per the <c>key = ( lcalpha / DIGIT ) 0*255 ( keychar )</c>
    /// production.
    /// </summary>
    private const int MaxKeyLength = 256;

    /// <summary>
    /// The maximum length of a value, per the <c>value = 0*255(chr) nblk-chr</c> production.
    /// </summary>
    private const int MaxValueLength = 256;

    private static readonly W3CTraceState Empty = new([]);

    private readonly Member[] members;

    private W3CTraceState(Member[] members)
    {
        this.members = members;
    }

    /// <summary>
    /// Parses a W3C <c>tracestate</c> value.
    /// </summary>
    /// <param name="tracestate">
    /// The <c>tracestate</c> value.
    /// </param>
    /// <returns>The parsed <see cref="W3CTraceState"/>. Parsing always succeeds.</returns>
    /// <remarks>
    /// A member that does not match the <c>list-member</c> grammar is kept as opaque text rather
    /// than reported as a failure: a caller handed a parse error would drop the whole header, and
    /// with it keys it never generated.
    /// <para/>
    /// The first 32 members are kept and the rest of the header is discarded, matching the limit
    /// the grammar puts on a <c>tracestate</c> list.
    /// </remarks>
    public static W3CTraceState Parse(string? tracestate) => ParseCore(tracestate, out _);

    /// <summary>
    /// Parses a W3C <c>tracestate</c> value.
    /// </summary>
    /// <param name="tracestate">
    /// The <c>tracestate</c> value.
    /// </param>
    /// <param name="state">
    /// When this method returns, the parsed <see cref="W3CTraceState"/>. It is populated the same
    /// way whichever value is returned, so <see langword="false"/> never yields less than
    /// <see langword="true"/> would. It is not necessarily everything the header carried: as with
    /// <c>Parse</c>, only the first 32 members are kept, and the rest are discarded on both
    /// branches.
    /// </param>
    /// <returns>
    /// <see langword="false"/> when members were retained and not one of them matched the
    /// <c>list-member</c> grammar; otherwise <see langword="true"/>.
    /// </returns>
    /// <remarks>
    /// The value reports on the members retained, not on the header as it arrived. A header of 32
    /// unusable members followed by a well-formed pair reports <see langword="false"/>, because the
    /// pair sits past the 32-member limit and was never taken on. Answering otherwise would mean
    /// reading past the bound that limit exists to enforce.
    /// <para/>
    /// Discarding well-formed members to stay inside that limit is not itself a failure: a header
    /// of 40 valid pairs keeps the first 32 and reports <see langword="true"/>.
    /// <para/>
    /// A header that is absent, empty or carries nothing but empty members reports
    /// <see langword="true"/>: there is nothing there to be wrong.
    /// <para/>
    /// A header with a mix of pairs and text kept verbatim also reports <see langword="true"/>. The
    /// signal is reserved for a value that is unusable end to end, which is the case a caller can
    /// act on; anything less would report on another vendor's spelling.
    /// </remarks>
    public static bool TryParse(string? tracestate, out W3CTraceState state)
    {
        state = ParseCore(tracestate, out var isValid);
        return isValid;
    }

    /// <summary>Looks up the value a key carries.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, the value associated with <paramref name="key"/>, or
    /// <see langword="null"/> when there is none.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the key is present; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Only members that match the <c>list-member</c> grammar can be addressed by key. A member
    /// kept verbatim is opaque text and is never returned, however it happens to be spelled.
    /// </remarks>
    public bool TryGetValue(string key, out string? value)
    {
        if (key is not null)
        {
            foreach (var member in this.members)
            {
                if (string.Equals(member.Key, key, StringComparison.Ordinal))
                {
                    value = member.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    /// <summary>Adds a key and its value, or replaces the value a key already carries.</summary>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    /// <returns>
    /// A new <see cref="W3CTraceState"/> with the modification applied, or the receiver itself when
    /// <paramref name="key"/> or <paramref name="value"/> is invalid.
    /// </returns>
    /// <remarks>
    /// The member written here is placed first, and every other member keeps its relative position.
    /// <para/>
    /// Every existing member carrying the key is collapsed into the one written here, so a header
    /// that arrived with the key twice comes back with it once.
    /// <para/>
    /// If setting the value would exceed the maximum of 32 members, the right-most value is dropped.
    /// </remarks>
    public W3CTraceState Set(string key, string value)
    {
        if (!IsValidKey(key.AsSpan()) || !IsValidValue(value.AsSpan()))
        {
            return this;
        }

        // Count what survives first, so the store is sized exactly and never trimmed afterwards.
        var kept = 0;
        foreach (var member in this.members)
        {
            if (!string.Equals(member.Key, key, StringComparison.Ordinal))
            {
                kept++;
            }
        }

        // The member written here takes the first slot, so a full list loses its last one.
        var members = new Member[Math.Min(kept + 1, MaxMembers)];
        members[0] = new Member(key, value);
        var written = 1;

        foreach (var member in this.members)
        {
            if (written == members.Length)
            {
                break;
            }

            if (!string.Equals(member.Key, key, StringComparison.Ordinal))
            {
                members[written++] = member;
            }
        }

        return new W3CTraceState(members);
    }

    /// <summary>
    /// Deletes the pair a key carries, if found.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <returns>
    /// A new <see cref="W3CTraceState"/> without <paramref name="key"/>, or the receiver itself when
    /// the key is absent or invalid.
    /// </returns>
    /// <remarks>
    /// Only members that match the <c>list-member</c> grammar can be addressed by key, so a member
    /// kept verbatim is never deleted.
    /// </remarks>
    public W3CTraceState Remove(string key)
    {
        if (!IsValidKey(key.AsSpan()))
        {
            return this;
        }

        // Count first: deleting a key that is not there then allocates nothing at all, which is what
        // a sampler asking on every span does.
        var matches = 0;
        foreach (var member in this.members)
        {
            if (string.Equals(member.Key, key, StringComparison.Ordinal))
            {
                matches++;
            }
        }

        if (matches == 0)
        {
            return this;
        }

        var members = new Member[this.members.Length - matches];
        var written = 0;

        foreach (var member in this.members)
        {
            if (!string.Equals(member.Key, key, StringComparison.Ordinal))
            {
                members[written++] = member;
            }
        }

        return new W3CTraceState(members);
    }

    /// <summary>
    /// Returns a string containing the W3C <c>tracestate</c> representation of the current instance.
    /// </summary>
    /// <returns>
    /// The serialized <c>tracestate</c>, or an empty string when there is nothing to emit.
    /// </returns>
    /// <remarks>
    /// No length limit is imposed: the 512 characters vendors SHOULD propagate is a floor on
    /// capability rather than a ceiling on output, and truncating to fit an external transport
    /// limit is the caller's decision.
    /// </remarks>
    public override string ToString()
    {
        if (this.members.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var member in this.members)
        {
            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            if (member.Key is not null)
            {
                builder.Append(member.Key)
                       .Append('=');
            }

            builder.Append(member.Value);
        }

        return builder.ToString();
    }

    private static W3CTraceState ParseCore(string? tracestate, out bool isValid)
    {
        // Nothing arrived, so there is nothing here that could be wrong.
        isValid = true;

        if (string.IsNullOrEmpty(tracestate))
        {
            return Empty;
        }

        var remaining = tracestate.AsSpan();
        var count = CountMembers(remaining);
        if (count == 0)
        {
            return Empty;
        }

        var members = new Member[count];
        var written = 0;
        isValid = false;

        while (!remaining.IsEmpty && written < count)
        {
            var comma = remaining.IndexOf(',');
            var member = remaining;

            if (comma >= 0)
            {
                member = member.Slice(0, comma);
                remaining = remaining.Slice(comma + 1);
            }
            else
            {
                remaining = default;
            }

            member = member.Trim();

            // Empty members are accepted but never re-emitted.
            if (member.IsEmpty)
            {
                continue;
            }

            var created = CreateMember(member);

            // One member matching the grammar is enough for the header to be valid: a vendor's
            // malformed entry is that vendor's business, not a fault in this header.
            isValid |= created.Key is not null;

            members[written++] = created;
        }

        return new W3CTraceState(members);

        static int CountMembers(ReadOnlySpan<char> header)
        {
            var count = 0;

            while (!header.IsEmpty && count < MaxMembers)
            {
                var comma = header.IndexOf(',');
                var member = header;

                if (comma >= 0)
                {
                    member = member.Slice(0, comma);
                    header = header.Slice(comma + 1);
                }
                else
                {
                    header = default;
                }

                member = member.Trim();

                if (!member.IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private static Member CreateMember(ReadOnlySpan<char> member)
    {
        var separator = member.IndexOf('=');
        if (separator > 0)
        {
            var key = member.Slice(0, separator);
            var value = member.Slice(separator + 1);

            if (IsValidKey(key) && IsValidValue(value))
            {
                return new Member(key.ToString(), value.ToString());
            }
        }

        // Malformed member: preserve it verbatim rather than discarding another vendor's data.
        return new Member(null, member.ToString());
    }

    private static bool IsValidKey(ReadOnlySpan<char> key)
    {
        // The flattened grammar of the later trace context levels is used, not the simple-key and
        // multi-tenant-key productions of level 1: it is a superset, and rejecting a key a level 1
        // parser would reject means deleting a key another vendor legitimately generated.
        if (key.IsEmpty || key.Length > MaxKeyLength)
        {
            return false;
        }

        if (!char.IsAsciiLetterLower(key[0]) && !char.IsAsciiDigit(key[0]))
        {
            return false;
        }

        foreach (var ch in key.Slice(1))
        {
            if (!char.IsAsciiLetterLower(ch) && !char.IsAsciiDigit(ch) &&
                ch is not '_' and not '-' and not '*' and not '/' and not '@')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidValue(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value.Length > MaxValueLength)
        {
            return false;
        }

        foreach (var ch in value)
        {
            // chr = %x20 / nblk-chr, and nblk-chr covers %x21-2B, %x2D-3C and %x3E-7E, so a comma
            // and an equals sign are the two printable characters excluded.
            if (ch is < ' ' or > '~' or ',' or '=')
            {
                return false;
            }
        }

        // The value ends in nblk-chr, so an interior space is allowed but a trailing one is not.
        return value[value.Length - 1] != ' ';
    }

    /// <summary>
    /// One member of a <c>tracestate</c> list: either a key and its value, or a run of text kept
    /// verbatim because it did not match the grammar.
    /// </summary>
    /// <param name="key">The key, or <see langword="null"/> for a member preserved verbatim.</param>
    /// <param name="value">
    /// The value when <paramref name="key"/> is non-<see langword="null"/>; otherwise the verbatim
    /// text of the member.
    /// </param>
    private readonly struct Member(string? key, string value)
    {
        public string? Key { get; } = key;

        public string Value { get; } = value;
    }
}
