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

    /// <summary>
    /// Parses the message the Kusto client writes when it starts an HTTP request.
    /// </summary>
    /// <param name="message">The trace message.</param>
    /// <param name="clientRequestId">
    /// The client request id written to the message, if it is known. When it is empty, the id is not known and the
    /// query text is only returned if it can be located without it.
    /// </param>
    /// <param name="requestBodyTraced">
    /// Whether the client is known to write the query text to the message. Only used when
    /// <paramref name="clientRequestId"/> is empty.
    /// </param>
    /// <returns>The parsed message.</returns>
    public static ParsedRequestStart ParseRequestStart(ReadOnlySpan<char> message, ReadOnlySpan<char> clientRequestId, bool requestBodyTraced = false)
    {
        var uri = ExtractValueBetween(message, "Uri=").ToString();
        _ = Uri.TryCreate(uri, UriKind.Absolute, out var parsed);
        var database = ExtractValueBetween(message, "DatabaseName=");
        var queryText = ExtractQueryText(message, clientRequestId, requestBodyTraced);

        // The port is only recorded when it is not the default port for the scheme.
        var port = parsed is { IsDefaultPort: false } ? parsed.Port : (int?)null;

        return new ParsedRequestStart(parsed?.DnsSafeHost, port, database, queryText);
    }

    public static ParsedException ParseException(ReadOnlySpan<char> message)
    {
        var errorMessage = ExtractValueBetween(message, "ErrorMessage=", newlineOnly: true);
        var errorType = ExtractValueBetween(message, "Exception object created: ", newlineOnly: true);
        return new ParsedException(errorMessage, errorType);
    }

    private static ReadOnlySpan<char> ExtractQueryText(ReadOnlySpan<char> message, ReadOnlySpan<char> clientRequestId, bool requestBodyTraced)
    {
        const string TextField = ", text=";

        // Query text may have embedded delimiters, however it is always the last field in the message.
        // The fields written before it (DatabaseName, ClientRequestId) are not escaped and may be supplied by
        // callers of the application, so they may contain "text=" themselves. Rather than searching for it, step
        // over the ClientRequestId field, whose value is known, and only accept the query text if the "text="
        // field immediately follows it. If the message does not have that shape, no query text is returned.
        var remaining = message.SliceAfter(", ClientRequestId=");

        if (clientRequestId.IsEmpty)
        {
            // The id is not known (the client request id may be overridden within an existing Kusto activity), so
            // the "text=" field can only be trusted if there is no other candidate. If the client request id or
            // the query itself contains ", text=" it is ambiguous which one starts the query, so return nothing.
            // Without the query text in the message, any ", text=" present must have been supplied by the caller.
            if (!requestBodyTraced)
            {
                return [];
            }

            var queryText = remaining.SliceAfter(TextField);
            return queryText.Contains(TextField, StringComparison.Ordinal) ? [] : queryText;
        }

        if (!remaining.StartsWith(clientRequestId))
        {
            return [];
        }

        remaining = remaining.Slice(clientRequestId.Length);

        // The client may write the IP addresses of the server between the ClientRequestId and text fields.
        if (remaining.StartsWith(", IpAddresses=("))
        {
            remaining = remaining.SliceAfter(")");
        }

        if (!remaining.StartsWith(TextField))
        {
            return [];
        }

        // The database name is also written unescaped, ahead of the client request id, so it could contain what
        // looks like the client request id field. If the same field appears again in what was taken to be the
        // query text, it is ambiguous which one is genuine, so return nothing.
        var text = remaining.Slice(TextField.Length);
        return ContainsClientRequestIdField(text, clientRequestId) ? [] : text;
    }

    private static bool ContainsClientRequestIdField(ReadOnlySpan<char> span, ReadOnlySpan<char> clientRequestId)
    {
        const string Field = ", ClientRequestId=";

        int index;
        while ((index = span.IndexOf(Field)) >= 0)
        {
            span = span.Slice(index + Field.Length);

            if (span.StartsWith(clientRequestId))
            {
                return true;
            }
        }

        return false;
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
