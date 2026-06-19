// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET9_0_OR_GREATER
using System.Buffers;
#endif

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Class that parses the delimited messages in <see cref="global::Kusto.Cloud.Platform.Utils.TraceRecord"/> instances.
/// </summary>
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
        _ = Uri.TryCreate(uri, UriKind.Absolute, out var parsed);
        var database = ExtractValueBetween(message, "DatabaseName=");

        // Query text may have embedded delimiters, however it is always the last field in the message
        // so we can just take everything after "text="
        var queryText = message.SliceAfter("text=");

        return new ParsedRequestStart(parsed?.Host, parsed?.Port, database, queryText);
    }

    public static ParsedException ParseException(ReadOnlySpan<char> message)
    {
        var errorMessage = ExtractValueBetween(message, "ErrorMessage=", newlineOnly: true);
        var errorType = ExtractValueBetween(message, "Exception object created: ", newlineOnly: true);
        return new ParsedException(errorMessage, errorType);
    }

    private static ReadOnlySpan<char> ExtractValueBetween(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle, bool newlineOnly = false)
    {
        var remaining = haystack.SliceAfter(needle);

        // Comma-delimited messages separate fields with commas, but newline-delimited messages (e.g. exception
        // payloads) can have values that legitimately contain commas, so those must only be split on newline.
        var endIndex = newlineOnly ? remaining.IndexOf('\n') : remaining.IndexOfAny(Delimiters);
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
        public readonly string? ServerAddress;
        public readonly int? ServerPort;
        public readonly ReadOnlySpan<char> Database;
        public readonly ReadOnlySpan<char> QueryText;

        public ParsedRequestStart(string? serverAddress, int? serverPort, ReadOnlySpan<char> database, ReadOnlySpan<char> queryText)
        {
            this.ServerAddress = serverAddress;
            this.ServerPort = serverPort;
            this.Database = database;
            this.QueryText = queryText;
        }
    }

    internal readonly ref struct ParsedException
    {
        public readonly ReadOnlySpan<char> ErrorMessage;
        public readonly ReadOnlySpan<char> ErrorType;

        public ParsedException(ReadOnlySpan<char> errorMessage, ReadOnlySpan<char> errorType)
        {
            this.ErrorMessage = errorMessage;
            this.ErrorType = errorType;
        }
    }
}
