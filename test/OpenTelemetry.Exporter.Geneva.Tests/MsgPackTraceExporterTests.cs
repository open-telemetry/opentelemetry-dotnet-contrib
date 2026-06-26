// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Geneva.MsgPack;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class MsgPackTraceExporterTests
{
    [Fact]
    public void CacheIfPartOfHttpUrl_KeyPresent_IndexInRange_SetsValueAndReturnsTrue()
    {
        var entry = new KeyValuePair<string, object?>("url.scheme", "https");
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var result = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry, arr);
        Assert.True(result);
        Assert.Equal("https", arr[0]);
    }

    [Fact]
    public void CacheIfPartOfHttpUrl_KeyPresent_IndexOutOfRange_ReturnsFalse()
    {
        var entry = new KeyValuePair<string, object?>("url.scheme", "https");
        var arr = Array.Empty<object?>(); // zero-length array
        var result = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry, arr);
        Assert.False(result);
    }

    [Fact]
    public void CacheIfPartOfHttpUrl_KeyNotPresent_ReturnsFalse()
    {
        var entry = new KeyValuePair<string, object?>("not.a.key", "value");
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var result = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry, arr);
        Assert.False(result);
    }

    [Fact]
    public void CacheIfPartOfHttpUrl_NullValue_SetsNull()
    {
        var entry = new KeyValuePair<string, object?>("url.scheme", null);
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var result = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry, arr);
        Assert.True(result);
        Assert.Null(arr[0]);
    }

    [Theory]
    [InlineData("", "", "", "", null)]
    [InlineData("http", "host", "", "", "http://host")]
    [InlineData("http", "host", "8080", "/x", "http://host:8080/x")]
    [InlineData("https", "server", "443", "/api", "https://server:443/api")]
    [InlineData("http", "host", "", "/x?y=1", "http://host/x?y=1")]
    public void GetHttpUrl_ReturnsExpectedUrl(string scheme, string hostOrAddress, string port, string pathAndQuery, string? expected)
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING_DICTIONARY.Count];
        arr[0] = scheme;
        arr[1] = hostOrAddress;
        arr[2] = string.IsNullOrEmpty(port) ? null : port;
        if (!string.IsNullOrEmpty(pathAndQuery) && pathAndQuery.Contains('?'))
        {
            var split = pathAndQuery.Split(['?'], 2);
            arr[3] = split[0];
            arr[4] = split.Length > 1 ? split[1] : null;
        }
        else
        {
            arr[3] = string.IsNullOrEmpty(pathAndQuery) ? null : pathAndQuery;
        }

        var url = MsgPackTraceExporter.GetHttpUrl(arr);
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("", "", "", "", null)]
    [InlineData("http", "host", "", "", "http://host")]
    [InlineData("http", "host", "8080", "/x", "http://host:8080/x")]
    [InlineData("https", "server", "443", "/api", "https://server:443/api")]
    [InlineData("http", "host", "", "/x?y=1", "http://host/x?y=1")]
    public void GetHttpUrl_QueryStartsWithQuestionMark_ReturnsExpectedUrl(string scheme, string hostOrAddress, string port, string pathAndQuery, string? expected)
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING_DICTIONARY.Count];
        arr[0] = scheme;
        arr[1] = hostOrAddress;
        arr[2] = string.IsNullOrEmpty(port) ? null : port;
        if (!string.IsNullOrEmpty(pathAndQuery))
        {
            var queryIndex = pathAndQuery.IndexOf('?');
            if (queryIndex == -1)
            {
                arr[3] = pathAndQuery;
                arr[4] = string.Empty;
            }
            else
            {
                arr[3] = pathAndQuery.Substring(0, queryIndex);
                arr[4] = pathAndQuery.Substring(queryIndex);
            }
        }
        else
        {
            arr[3] = null;
        }

        var url = MsgPackTraceExporter.GetHttpUrl(arr);
        Assert.Equal(expected, url);
    }

    [Fact]
    public void GetHttpUrl_UnknownMethod_ReturnsNull()
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var url = MsgPackTraceExporter.GetHttpUrl(arr);
        Assert.Null(url);
    }

    [Fact]
    public void SerializeHttpUrl_MatchesGetHttpUrlPlusSerializer_ByteForByte()
    {
        var count = MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count;

        object?[] Parts(object? scheme, object? address, object? port, object? path, object? query)
        {
            var arr = new object?[count];
            arr[0] = scheme;
            arr[1] = address;
            arr[2] = port;
            arr[3] = path;
            arr[4] = query;
            return arr;
        }

        var cases = new[]
        {
            Parts(null, null, null, null, null),                          // no parts => nothing written
            Parts("http", "host", null, null, null),                      // scheme + address only
            Parts("http", "host", "8080", "/x", "y=1"),                   // query without '?'
            Parts("http", "host", "8080", "/x", "?y=1"),                  // query with leading '?'
            Parts("http", "host", "8080", "/x", "?"),                     // query is just '?'
            Parts("http", "host", "8080", "/x", string.Empty),           // empty query => omitted
            Parts("http", "host", string.Empty, "/x", "a=b"),             // port "" (non-null) => ':' still emitted
            Parts("http", "host", 8080, "/x", "a=b"),                     // numeric (boxed) port
            Parts("https", "\u00FCn\u00EE\u00E7\u00F8d\u00E9.example", "443", "/p\u00E2th/\u00E7", "q=\u00FC"), // unicode
            Parts(null, "host", null, "/p", null),                        // missing scheme
            Parts("http", null, "80", null, null),                        // missing address/path
            Parts("http", new string('a', 20000), null, null, null),      // > 16383 chars => truncation fallback path
        };

        foreach (var parts in cases)
        {
            var url = MsgPackTraceExporter.GetHttpUrl(parts);

            var expected = new byte[65360];
            var expectedCursor = 0;
            if (url != null)
            {
                expectedCursor = MessagePackSerializer.SerializeAsciiString(expected, expectedCursor, "httpUrl");
                expectedCursor = MessagePackSerializer.SerializeUnicodeString(expected, expectedCursor, url);
            }

            var actual = new byte[65360];
            ushort cntFields = 0;
            var actualCursor = MsgPackTraceExporter.SerializeHttpUrl(actual, 0, parts, ref cntFields);

            Assert.Equal(expectedCursor, actualCursor);
            Assert.Equal(url != null ? 1 : 0, cntFields);
            Assert.Equal(expected.AsSpan(0, expectedCursor).ToArray(), actual.AsSpan(0, actualCursor).ToArray());
        }
    }
}
