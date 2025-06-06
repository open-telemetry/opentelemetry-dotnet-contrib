// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Geneva.MsgPack;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class MsgPackTraceExporterTests
{
    [Fact]
    public void CacheIfPartOfHttpUrl_KeyPresent_IndexInRange_SetsValueAndReturnsTrue()
    {
        var entry = new KeyValuePair<string, object?>("http.scheme", "https");
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var result = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry, arr);
        Assert.True(result);
        Assert.Equal("https", arr[0]);
    }

    [Fact]
    public void CacheIfPartOfHttpUrl_KeyPresent_IndexOutOfRange_ReturnsFalse()
    {
        var entry = new KeyValuePair<string, object?>("http.scheme", "https");
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
        var entry = new KeyValuePair<string, object?>("http.scheme", null);
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var result = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry, arr);
        Assert.True(result);
        Assert.Null(arr[0]);
    }

    [Theory]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V1, "", "", "", "", "://")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V1, "http", "host", "", "", "http://host")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V1, "http", "host", "8080", "", "http://host:8080")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V1, "http", "host", "", "/path", "http://host/path")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V1, "https", "example.com", "443", "/foo/bar", "https://example.com:443/foo/bar")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V1, "http", "host", "80", "/x?y=1", "http://host:80/x?y=1")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V2, "", "", "", "", "://")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V2, "http", "host", "", "", "http://host")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V2, "http", "host", "8080", "/x", "http://host:8080/x")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V2, "https", "server", "443", "/api", "https://server:443/api")]
    [InlineData(MsgPackTraceExporter.HTTP_METHOD_V2, "http", "host", "", "/x?y=1", "http://host/x?y=1")]
    public void GetHttpUrl_ReturnsExpectedUrl(string method, string scheme, string hostOrAddress, string port, string pathOrTarget, string expected)
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_MAPPING_DICTIONARY.Count];
        if (method == MsgPackTraceExporter.HTTP_METHOD_V1)
        {
            arr[0] = scheme;
            arr[1] = hostOrAddress;
            arr[2] = string.IsNullOrEmpty(port) ? null : port;
            arr[3] = string.IsNullOrEmpty(pathOrTarget) ? null : pathOrTarget;
        }
        else if (method == MsgPackTraceExporter.HTTP_METHOD_V2)
        {
            arr[4] = scheme;
            arr[5] = hostOrAddress;
            arr[6] = string.IsNullOrEmpty(port) ? null : port;
            if (!string.IsNullOrEmpty(pathOrTarget) && pathOrTarget.Contains('?'))
            {
                var split = pathOrTarget.Split(['?'], 2);
                arr[7] = split[0];
                arr[8] = split.Length > 1 ? split[1] : null;
            }
            else
            {
                arr[7] = string.IsNullOrEmpty(pathOrTarget) ? null : pathOrTarget;
            }
        }

        var url = MsgPackTraceExporter.GetHttpUrl(method, arr);
        Assert.Equal(expected, url);
    }

    [Fact]
    public void GetHttpUrl_UnknownMethod_ReturnsNull()
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var url = MsgPackTraceExporter.GetHttpUrl("not.a.method", arr);
        Assert.Null(url);
    }
}
