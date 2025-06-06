// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Extensions.Trace.PartialActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class SpecHelperTests
{
    [Fact]
    public void ToUnixTimeNanoseconds_ShouldConvertDateTimeCorrectly()
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = SpecHelper.ToUnixTimeNanoseconds(dateTime);

        Assert.Equal(0UL, result);
    }

    [Fact]
    public void ToUnixTimeNanoseconds_ShouldConvertDateTimeOffsetCorrectly()
    {
        var dateTimeOffset = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = SpecHelper.ToUnixTimeNanoseconds(dateTimeOffset);

        Assert.Equal(0UL, result);
    }

    [Fact]
    public void Json_ShouldContainExpectedKeys()
    {
        var tracesData = new TracesData(new Activity("TestActivity"), TracesData.Signal.Start);

        var json = SpecHelper.Json(tracesData);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("resource_spans", out var resourceSpans));
        Assert.True(resourceSpans[0].TryGetProperty("scope_spans", out var scopeSpans));
        Assert.True(scopeSpans[0].TryGetProperty("scope", out var scope));
        Assert.True(scope.TryGetProperty("name", out _));
        Assert.True(scope.TryGetProperty("version", out _));
        Assert.True(scope.TryGetProperty("attributes", out _));
        Assert.True(scopeSpans[0].TryGetProperty("spans", out var spans));
        Assert.True(spans[0].TryGetProperty("trace_id", out _));
        Assert.True(spans[0].TryGetProperty("span_id", out _));
        Assert.True(spans[0].TryGetProperty("trace_state", out _));
        Assert.True(spans[0].TryGetProperty("parent_span_id", out _));
        Assert.True(spans[0].TryGetProperty("flags", out _));
        Assert.True(spans[0].TryGetProperty("name", out _));
        Assert.True(spans[0].TryGetProperty("kind", out _));
        Assert.True(spans[0].TryGetProperty("start_time_unix_nano", out _));
        Assert.True(spans[0].TryGetProperty("attributes", out _));
        Assert.True(spans[0].TryGetProperty("events", out _));
        Assert.True(spans[0].TryGetProperty("links", out _));
        Assert.True(spans[0].TryGetProperty("status", out var status));
        Assert.True(status.TryGetProperty("code", out _));
    }
}
