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
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_MAPPING_DICTIONARY.Count];
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

    [Fact]
    public void GetHttpUrl_UnknownMethod_ReturnsNull()
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var url = MsgPackTraceExporter.GetHttpUrl(arr);
        Assert.Null(url);
    }

    [Theory]
    [InlineData("url.scheme")]
    [InlineData("server.address")]
    [InlineData("server.port")]
    [InlineData("url.path")]
    [InlineData("url.query")]
    public void CacheIfPartOfHttpUrl_AllValidKeys_ReturnTrue(string key)
    {
        var entry = new KeyValuePair<string, object?>(key, "value");
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        var result = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry, arr);
        Assert.True(result);
        Assert.Equal("value", arr[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING[key]]);
    }

    [Fact]
    public void GetHttpUrl_EmptyStrings_ReturnsNull()
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        arr[0] = string.Empty;
        arr[1] = string.Empty;
        arr[2] = null;
        arr[3] = null;
        arr[4] = null;

        var url = MsgPackTraceExporter.GetHttpUrl(arr);
        Assert.Null(url);
    }

    [Fact]
    public void GetHttpUrl_ComplexPath_HandlesCorrectly()
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        arr[0] = "https";
        arr[1] = "api.example.com";
        arr[2] = null;
        arr[3] = "/v1/users/123/orders/456";
        arr[4] = null;

        var url = MsgPackTraceExporter.GetHttpUrl(arr);
        Assert.Equal("https://api.example.com/v1/users/123/orders/456", url);
    }

    [Fact]
    public void GetHttpUrl_ComplexQuery_HandlesCorrectly()
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];
        arr[0] = "https";
        arr[1] = "search.example.com";
        arr[2] = null;
        arr[3] = "/search";
        arr[4] = "q=test&sort=desc&page=2&filter=active";

        var url = MsgPackTraceExporter.GetHttpUrl(arr);
        Assert.Equal("https://search.example.com/search?q=test&sort=desc&page=2&filter=active", url);
    }

    [Fact]
    public void CacheIfPartOfHttpUrl_MultipleCallsSameArray_OverwritesPreviousValue()
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];

        var entry1 = new KeyValuePair<string, object?>("url.scheme", "http");
        var result1 = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry1, arr);
        Assert.True(result1);
        Assert.Equal("http", arr[0]);

        var entry2 = new KeyValuePair<string, object?>("url.scheme", "https");
        var result2 = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry2, arr);
        Assert.True(result2);
        Assert.Equal("https", arr[0]);
    }

    [Fact]
    public void CacheIfPartOfHttpUrl_DifferentKeys_PopulatesDifferentIndices()
    {
        var arr = new object?[MsgPackTraceExporter.CS40_PART_B_HTTPURL_MAPPING.Count];

        var entries = new[]
        {
            new KeyValuePair<string, object?>("url.scheme", "https"),
            new KeyValuePair<string, object?>("server.address", "example.com"),
            new KeyValuePair<string, object?>("server.port", 443),
            new KeyValuePair<string, object?>("url.path", "/api"),
            new KeyValuePair<string, object?>("url.query", "key=value"),
        };

        foreach (var entry in entries)
        {
            var result = MsgPackTraceExporter.CacheIfPartOfHttpUrl(entry, arr);
            Assert.True(result);
        }

        Assert.Equal("https", arr[0]);
        Assert.Equal("example.com", arr[1]);
        Assert.Equal(443, arr[2]);
        Assert.Equal("/api", arr[3]);
        Assert.Equal("key=value", arr[4]);
    }
}
