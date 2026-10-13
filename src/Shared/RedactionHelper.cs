// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Internal;

internal sealed class RedactionHelper
{
    private const string RedactedText = "Redacted";

    public static string? GetRedactedQueryString(string query)
    {
        var index =
#if NET
            query.IndexOf('=', StringComparison.Ordinal);
#else
            query.IndexOf('=');
#endif

        if (index == -1)
        {
            return query;
        }

        var length = query.Length;

        // Preallocate some size to avoid re-sizing multiple times.
        // Since the size will increase, allocating twice as much.
        using ValueStringBuilder queryBuilder = new(2 * length);
        queryBuilder.Append(query.AsSpan(0, index));
        while (index < query.Length)
        {
            // Check if the character is = for redacting value.
            if (query[index] == '=')
            {
                // Append =
                queryBuilder.Append('=');
                index++;

                // Append redactedText in place of original value.
                queryBuilder.Append(RedactedText);

                // Move until end of this key/value pair.
                while (index < length && query[index] != '&')
                {
                    index++;
                }

                // End of key/value.
                if (index < length && query[index] == '&')
                {
                    queryBuilder.Append(query[index]);
                }
            }
            else
            {
                // Keep adding to the result
                queryBuilder.Append(query[index]);
            }

            index++;
        }

        return queryBuilder.ToString();
    }

    /// <summary>
    /// Redacts the values of the query string found between <paramref name="queryStart"/>
    /// (inclusive) and <paramref name="queryEnd"/> (exclusive) in <paramref name="value"/>.
    /// Any characters before or after the query string are copied verbatim.
    /// <para/>
    /// The query string is redacted in the same way as <see cref="GetRedactedQueryString(string)"/>,
    /// but without having to allocate a substring for it first.
    /// </summary>
    /// <param name="value">The string containing the query string, such as an absolute URI.</param>
    /// <param name="queryStart">The index of the first character of the query string.</param>
    /// <param name="queryEnd">The index of the first character after the end of the query string.</param>
    /// <returns>
    /// <paramref name="value"/> with the query string values redacted, or <paramref name="value"/>
    /// itself if the query string does not contain any values to redact.
    /// </returns>
    public static string GetRedactedQueryString(string value, int queryStart, int queryEnd)
    {
        Debug.Assert(queryStart >= 0 && queryStart <= queryEnd && queryEnd <= value.Length, "The query string bounds must be within the value.");

        var query = value.AsSpan(queryStart, queryEnd - queryStart);
        var index = query.IndexOf('=');

        if (index == -1)
        {
            return value;
        }

        var length = query.Length;

        // Preallocate some size to avoid re-sizing multiple times.
        // Since the size of the query string will increase, allocating twice as much for it.
        using ValueStringBuilder queryBuilder = new(value.Length + length);
        queryBuilder.Append(value.AsSpan(0, queryStart + index));
        while (index < query.Length)
        {
            // Check if the character is = for redacting value.
            if (query[index] == '=')
            {
                // Append =
                queryBuilder.Append('=');
                index++;

                // Append redactedText in place of original value.
                queryBuilder.Append(RedactedText);

                // Move until end of this key/value pair.
                while (index < length && query[index] != '&')
                {
                    index++;
                }

                // End of key/value.
                if (index < length && query[index] == '&')
                {
                    queryBuilder.Append(query[index]);
                }
            }
            else
            {
                // Keep adding to the result
                queryBuilder.Append(query[index]);
            }

            index++;
        }

        queryBuilder.Append(value.AsSpan(queryEnd));

        return queryBuilder.ToString();
    }

    // Simplified version of System.Text.ValueStringBuilder from .NET runtime
    // https://github.com/dotnet/runtime/blob/7db43828cc273aa164f2247744a43f70555f780f/src/libraries/Common/src/System/Text/ValueStringBuilder.cs,
    // keeping only the members this type actually uses, to avoid the heap
    // allocation and copying overhead of System.Text.StringBuilder.
    private ref struct ValueStringBuilder
    {
        private char[]? arrayToReturnToPool;
        private Span<char> chars;
        private int pos;

        public ValueStringBuilder(int initialCapacity)
        {
            this.arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
            this.chars = this.arrayToReturnToPool;
            this.pos = 0;
        }

        public override string ToString()
        {
            var s = this.chars.Slice(0, this.pos).ToString();
            this.Dispose();
            return s;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char c)
        {
            var currentPos = this.pos;
            var currentChars = this.chars;
            if ((uint)currentPos < (uint)currentChars.Length)
            {
                currentChars[currentPos] = c;
                this.pos = currentPos + 1;
            }
            else
            {
                this.GrowAndAppend(c);
            }
        }

#if !NET
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string? s)
        {
            if (s == null)
            {
                return;
            }

            this.Append(s.AsSpan());
        }
#endif

        public void Append(scoped ReadOnlySpan<char> value)
        {
            var currentPos = this.pos;
            if (currentPos > this.chars.Length - value.Length)
            {
                this.Grow(value.Length);
            }

            value.CopyTo(this.chars.Slice(this.pos));
            this.pos += value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var toReturn = this.arrayToReturnToPool;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowAndAppend(char c)
        {
            this.Grow(1);
            this.Append(c);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int additionalCapacityBeyondPos)
        {
            Debug.Assert(additionalCapacityBeyondPos > 0, "additionalCapacityBeyondPos must be positive.");
            Debug.Assert(this.pos > this.chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

            const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

            var newCapacity = (int)Math.Max(
                (uint)(this.pos + additionalCapacityBeyondPos),
                Math.Min((uint)this.chars.Length * 2, ArrayMaxLength));

            var poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

            this.chars.Slice(0, this.pos).CopyTo(poolArray);

            var toReturn = this.arrayToReturnToPool;
            this.chars = this.arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }
    }
}
