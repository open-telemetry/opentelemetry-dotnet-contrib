// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.Trace;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class PartialActivityProcessorTests
{
    private const int ScheduledDelayMilliseconds = 1000;
    private readonly List<LogRecord> exportedLogs = [];
    private readonly PartialActivityProcessor processor;

    public PartialActivityProcessorTests()
    {
        InMemoryExporter<LogRecord> logExporter1 = new InMemoryExporter<LogRecord>(this.exportedLogs);
        this.processor = new PartialActivityProcessor(logExporter1, ScheduledDelayMilliseconds);
    }

    [Fact]
    public void Constructor_ShouldInitializeFields() => Assert.NotNull(this.processor);

    [Fact]
    public void OnStart_ShouldExportHeartbeatLog()
    {
        var activity = new Activity("TestActivity");

        this.processor.OnStart(activity);

        Assert.Contains(activity.SpanId, this.processor.ActiveActivities);
        Assert.Single(this.exportedLogs);
    }

    [Fact]
    public void OnEnd_ShouldExportStopLog()
    {
        var activity = new Activity("TestActivity");

        this.processor.OnStart(activity);

        this.processor.OnEnd(activity);

        Assert.Contains(activity.SpanId, this.processor.ActiveActivities);
        Assert.Contains(
            new KeyValuePair<ActivitySpanId, Activity>(activity.SpanId, activity),
            this.processor.EndedActivities);
        Assert.Equal(2, this.exportedLogs.Count);
    }

    [Fact]
    public void OnEndAfterHeartbeat_ShouldCleanupActivity()
    {
        var activity = new Activity("TestActivity");

        this.processor.OnStart(activity);

        this.processor.OnEnd(activity);

        Thread.Sleep(ScheduledDelayMilliseconds);

        Assert.DoesNotContain(activity.SpanId, this.processor.ActiveActivities);
        Assert.DoesNotContain(
            new KeyValuePair<ActivitySpanId, Activity>(activity.SpanId, activity),
            this.processor.EndedActivities);
        Assert.Equal(2, this.exportedLogs.Count);
    }

    [Fact]
    public void Heartbeat_ShouldExportLogRecords()
    {
        var activity = new Activity("TestActivity");

        this.processor.OnStart(activity);

        Assert.Single(this.exportedLogs);
        Thread.Sleep(ScheduledDelayMilliseconds);
        Assert.Equal(2, this.exportedLogs.Count);
        Thread.Sleep(ScheduledDelayMilliseconds);
        Assert.Equal(3, this.exportedLogs.Count);
    }
}
