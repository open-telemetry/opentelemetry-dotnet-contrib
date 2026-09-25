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
/// The instance is always a valid <c>tracestate</c>: a member that is malformed, or is a repeated
/// key, is discarded as the header is parsed.
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

    /// <summary>
    /// The characters ignored around a member, per <c>OWS = *( SP / HTAB )</c>.
    /// </summary>
    private const string OptionalWhitespace = " \t";

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
    /// A member that is not a well-formed key-value pair, or that repeats a key, is discarded; use
    /// <see cref="TryParse(string, out W3CTraceState)"/> to learn whether that happened. Once 32
    /// members are kept, the rest of the header is dropped.
    /// </remarks>
    public static W3CTraceState Parse(string? tracestate) => ParseCore(tracestate, out _);

    /// <summary>
    /// Parses a W3C <c>tracestate</c> value and reports whether an invalid or repeated member was
    /// discarded.
    /// </summary>
    /// <param name="tracestate">
    /// The <c>tracestate</c> value.
    /// </param>
    /// <param name="state">
    /// When this method returns, the parsed <see cref="W3CTraceState"/>, populated the same way
    /// whichever value is returned.
    /// </param>
    /// <returns>
    /// <see langword="false"/> when a member was discarded because it was not a well-formed
    /// key-value pair or repeated a key; otherwise <see langword="true"/>.
    /// </returns>
    /// <remarks>
    /// Once 32 members are kept the rest of the header is dropped without being examined, so a
    /// header of 40 valid pairs keeps the first 32 and reports <see langword="true"/>. A header that
    /// is absent, empty or carries only empty members also reports <see langword="true"/>.
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

            builder.Append(member.Key)
                   .Append('=')
                   .Append(member.Value);
        }

        return builder.ToString();
    }

    private static W3CTraceState ParseCore(string? tracestate, out bool isValid)
    {
        isValid = true;

        if (string.IsNullOrEmpty(tracestate))
        {
            return Empty;
        }

        var remaining = tracestate.AsSpan();
        var count = CountMembers(remaining);
        var members = count == 0 ? [] : new Member[count];
        var written = 0;

        while (!remaining.IsEmpty && written < MaxMembers)
        {
            var member = NextMember(ref remaining);

            // Empty members are accepted but never re-emitted.
            if (member.IsEmpty)
            {
                continue;
            }

            if (!TryParseMember(member, out var key, out var value) || ContainsKey(members.AsSpan(0, written), key))
            {
                isValid = false;
                continue;
            }

            members[written++] = new Member(key.ToString(), value.ToString());
        }

        if (written == 0)
        {
            return Empty;
        }

        // A repeated key was counted but not kept, so the store is longer than the list.
        if (written < members.Length)
        {
            Array.Resize(ref members, written);
        }

        return new W3CTraceState(members);

        static int CountMembers(ReadOnlySpan<char> header)
        {
            var count = 0;

            while (!header.IsEmpty && count < MaxMembers)
            {
                if (TryParseMember(NextMember(ref header), out _, out _))
                {
                    count++;
                }
            }

            return count;
        }

        static ReadOnlySpan<char> NextMember(ref ReadOnlySpan<char> header)
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

            return member.Trim(OptionalWhitespace.AsSpan());
        }

        static bool ContainsKey(ReadOnlySpan<Member> members, ReadOnlySpan<char> key)
        {
            foreach (var member in members)
            {
                if (key.Equals(member.Key.AsSpan(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static bool TryParseMember(
        ReadOnlySpan<char> member,
        out ReadOnlySpan<char> key,
        out ReadOnlySpan<char> value)
    {
        var separator = member.IndexOf('=');
        if (separator > 0)
        {
            key = member.Slice(0, separator);
            value = member.Slice(separator + 1);
            return IsValidKey(key) && IsValidValue(value);
        }

        key = default;
        value = default;
        return false;
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

    /// <summary>One key-value pair of a <c>tracestate</c> list.</summary>
    private readonly struct Member(string key, string value)
    {
        public string Key { get; } = key;

        public string Value { get; } = value;
    }
}
