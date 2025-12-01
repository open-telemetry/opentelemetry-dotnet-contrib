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
        var uri = ExtractValueBetween(message, "Uri=").ToString();
        Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed);
        var database = ExtractValueBetween(message, "DatabaseName=");

        // Query text may have embedded delimiters, however it is always the last field in the message
        // so we can just take everything after "text="
        var queryText = message.SliceAfter("text=");

        return new ParsedRequestStart(uri, parsed?.Host, parsed?.Port, database, queryText);
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
        var remaining = haystack.SliceAfter(needle);

        var endIndex = remaining.IndexOfAny(Delimiters);
        if (endIndex < 0)
        {
            endIndex = remaining.Length;
        }

        var result = remaining.Slice(0, endIndex);
        result = result.Trim(); // Trim to specifically handle newlines, which may be multiple characters

        return result;
    }

    internal readonly ref struct ParsedRequestStart
    {
        public readonly string Uri;
        public readonly string? ServerAddress;
        public readonly int? ServerPort;
        public readonly ReadOnlySpan<char> Database;
        public readonly ReadOnlySpan<char> QueryText;

        public ParsedRequestStart(string uri, string? serverAddress, int? serverPort, ReadOnlySpan<char> database, ReadOnlySpan<char> queryText)
        {
            this.Uri = uri;
            this.ServerAddress = serverAddress;
            this.ServerPort = serverPort;
            this.Database = database;
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
