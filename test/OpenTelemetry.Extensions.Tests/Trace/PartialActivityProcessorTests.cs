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
    private readonly List<LogRecord> exportedLogs = [];
    private readonly PartialActivityProcessor processor;

    public PartialActivityProcessorTests()
    {
        InMemoryExporter<LogRecord>
            logExporter = new InMemoryExporter<LogRecord>(this.exportedLogs);
        this.processor = new PartialActivityProcessor(logExporter);
    }

    [Fact]
    public void Constructor_ShouldInitializeFields() => Assert.NotNull(this.processor);

    [Fact]
    public void OnStart_ShouldExportStartLog()
    {
        var activity = new Activity("TestActivity");

        this.processor.OnStart(activity);

        Assert.Single(this.exportedLogs);
    }

    [Fact]
    public void OnEnd_ShouldExportEndLog()
    {
        var activity = new Activity("TestActivity");

        this.processor.OnStart(activity);

        this.processor.OnEnd(activity);

        Assert.Equal(2, this.exportedLogs.Count);
    }
}
