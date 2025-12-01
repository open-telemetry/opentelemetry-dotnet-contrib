// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET9_0_OR_GREATER
using System.Buffers;
#endif

using System.Globalization;

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
        GetServerAddressAndPort(uri, out var serverAddress, out var serverPort);
        var database = ExtractValueBetween(message, "DatabaseName=");

        // Query text may have embedded delimiters, however it is always the last field in the message
        // so we can just take everything after "text="
        var queryText = message.SliceAfter("text=");

        return new ParsedRequestStart(uri, serverAddress, serverPort, database, queryText);
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

    private static void GetServerAddressAndPort(ReadOnlySpan<char> uri, out ReadOnlySpan<char> serverAddress, out int? serverPort)
    {
        var hostAndPort = uri.SliceAfter("://");
        hostAndPort = hostAndPort.SliceBefore(['/']);

        // Find the port separator (last colon for IPv6 compatibility)
        var colonIndex = hostAndPort.LastIndexOf(':');
        if (colonIndex > 0)
        {
            // Check if this is an IPv6 address (contains '[' and ']')
            var openBracketIndex = hostAndPort.IndexOf('[');
            var closeBracketIndex = hostAndPort.IndexOf(']');

            if (openBracketIndex >= 0 && closeBracketIndex > openBracketIndex && colonIndex > closeBracketIndex)
            {
                // IPv6 address with port: [2001:db8::1]:8080
                serverAddress = hostAndPort.Slice(0, colonIndex);
#if NET
                serverPort = int.Parse(hostAndPort.Slice(colonIndex + 1), CultureInfo.InvariantCulture);
#else
                serverPort = int.Parse(hostAndPort.Slice(colonIndex + 1).ToString(), CultureInfo.InvariantCulture);
#endif
            }
            else if (openBracketIndex < 0)
            {
                // IPv4 or hostname with port: localhost:8080
                serverAddress = hostAndPort.Slice(0, colonIndex);
#if NET
                serverPort = int.Parse(hostAndPort.Slice(colonIndex + 1), CultureInfo.InvariantCulture);
#else
                serverPort = int.Parse(hostAndPort.Slice(colonIndex + 1).ToString(), CultureInfo.InvariantCulture);
#endif
            }
            else
            {
                // IPv6 address without port: [2001:db8::1]
                serverAddress = hostAndPort;
                serverPort = null;
            }
        }
        else
        {
            // No port specified
            serverAddress = hostAndPort;
            serverPort = null;
        }
    }

    internal readonly ref struct ParsedRequestStart
    {
        public readonly ReadOnlySpan<char> Uri;
        public readonly ReadOnlySpan<char> ServerAddress;
        public readonly int? ServerPort;
        public readonly ReadOnlySpan<char> Database;
        public readonly ReadOnlySpan<char> QueryText;

        public ParsedRequestStart(ReadOnlySpan<char> uri, ReadOnlySpan<char> serverAddress, int? serverPort, ReadOnlySpan<char> database, ReadOnlySpan<char> queryText)
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
