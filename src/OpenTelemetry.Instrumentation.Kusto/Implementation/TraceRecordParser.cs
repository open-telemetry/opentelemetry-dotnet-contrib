// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET9_0_OR_GREATER
using System.Buffers;
#endif

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal class TraceRecordParser
{
#if NET9_0_OR_GREATER
    private static readonly SearchValues<char> Delimiters = SearchValues.Create([',', '\n']);
#else
    private static readonly char[] Delimiters = [',', '\n'];
#endif

    public static ParsedRequestStart ParseRequestStart(ReadOnlySpan<char> message)
    {
        var uri = ExtractValueBetween(message, "Uri=");
        var host = GetServerAddress(uri);
        var queryText = ExtractValueBetween(message, "text=");

        return new ParsedRequestStart(uri, host, queryText);
    }

    public static ParsedActivityComplete ParseActivityComplete(ReadOnlySpan<char> message)
    {
        var howEnded = ExtractValueBetween(message, "HowEnded=");
        return new ParsedActivityComplete(howEnded);
    }

    public static ParsedException ParseException(ReadOnlySpan<char> message)
    {
        var errorMessage = ExtractValueBetween(message, "ErrorMessage=");
        return new ParsedException(errorMessage);
    }

    private static ReadOnlySpan<char> ExtractValueBetween(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle)
    {
        var startIndex = haystack.IndexOf(needle);
        if (startIndex < 0)
        {
            return ReadOnlySpan<char>.Empty;
        }

        startIndex += needle.Length;
        var remaining = haystack.Slice(startIndex);

        var endIndex = remaining.IndexOfAny(Delimiters);
        if (endIndex < 0)
        {
            endIndex = remaining.Length;
        }

        var result = remaining.Slice(0, endIndex);
        result = result.Trim(); // Trim to specifically handle newlines, which may be multiple characters

        return result;
    }

    private static ReadOnlySpan<char> GetServerAddress(ReadOnlySpan<char> uri)
    {
        var schemeEnd = uri.IndexOf("://".AsSpan());
        if (schemeEnd < 0)
        {
            return ReadOnlySpan<char>.Empty;
        }

        var hostStart = schemeEnd + 3;
        var remaining = uri.Slice(hostStart);

        var pathStart = remaining.IndexOf('/');
        var host = pathStart >= 0 ? remaining.Slice(0, pathStart) : remaining;

        return host;
    }

    internal readonly ref struct ParsedRequestStart
    {
        public readonly ReadOnlySpan<char> Uri;
        public readonly ReadOnlySpan<char> Host;
        public readonly ReadOnlySpan<char> QueryText;

        public ParsedRequestStart(ReadOnlySpan<char> uri, ReadOnlySpan<char> host, ReadOnlySpan<char> queryText)
        {
            this.Uri = uri;
            this.Host = host;
            this.QueryText = queryText;
        }
    }

    internal readonly ref struct ParsedActivityComplete
    {
        public readonly ReadOnlySpan<char> HowEnded;

        public ParsedActivityComplete(ReadOnlySpan<char> howEnded)
        {
            this.HowEnded = howEnded;
        }
    }

    internal readonly ref struct ParsedException
    {
        public readonly ReadOnlySpan<char> ErrorMessage;

        public ParsedException(ReadOnlySpan<char> errorMessage)
        {
            this.ErrorMessage = errorMessage;
        }
    }
}
