// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Extensions.Trace.PartialActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class TracesDataTests
{
    [Fact]
    public void Json_ShouldSerializeActivitySpecToSnakeCaseJson()
    {
        var activity = new Activity("TestActivity");
        activity.Start();
        activity.Stop();

        var tracesData = new TracesData(activity, TracesData.Signal.Ended);
        var json = SpecHelper.Json(tracesData);

        Assert.Contains("\"resource_spans\":[", json);
        Assert.Contains("\"scope_spans\":[", json);
        Assert.Contains("\"trace_id\":", json);
        Assert.Contains("\"span_id\":", json);
        Assert.Contains("\"trace_state\":", json);
        Assert.Contains("\"parent_span_id\":", json);
        Assert.Contains("\"flags\":", json);
        Assert.Contains("\"name\":", json);
        Assert.Contains("\"kind\":", json);
        Assert.Contains("\"start_time_unix_nano\":", json);
        Assert.Contains("\"end_time_unix_nano\":", json);
        Assert.Contains("\"attributes\":[", json);
        Assert.Contains("\"events\":[", json);
        Assert.Contains("\"links\":[", json);
        Assert.Contains("\"status\":{", json);
        Assert.Contains("\"code\":", json);
    }
}
