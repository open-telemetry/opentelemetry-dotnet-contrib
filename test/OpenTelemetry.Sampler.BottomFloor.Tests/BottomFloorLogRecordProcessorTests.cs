// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using CapturedRecord = OpenTelemetry.Sampler.BottomFloor.Tests.BottomFloorLogExporterTests.CapturedRecord;
using CapturingExporter = OpenTelemetry.Sampler.BottomFloor.Tests.BottomFloorLogExporterTests.CapturingExporter;

namespace OpenTelemetry.Sampler.BottomFloor.Tests;

[Collection("LogPipeline")]
public class BottomFloorLogRecordProcessorTests
{
    [Fact]
    public void Constructor_RejectsNullInnerExporter()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BottomFloorLogRecordProcessor(null!, new BottomFloorLogSamplerOptions()));
    }

    [Fact]
    public void Constructor_RejectsNullOptions()
    {
        using var inner = new CapturingExporter(new List<CapturedRecord>());
        Assert.Throws<ArgumentNullException>(() => new BottomFloorLogRecordProcessor(inner, null!));
    }

    [Fact]
    public void FullWindow_ForwardsExactlyBudgetRecords()
    {
        // The processor packages the batch-plus-reservoir wiring: a window is one
        // export batch, sized above the budget so the reservoir bounds the output.
        const int budget = 10;
        const int arrivals = 500;
        var captured = new List<CapturedRecord>();
        var options = new BottomFloorLogSamplerOptions { Budget = budget };

        using var inner = new CapturingExporter(captured);
        using (var processor = new BottomFloorLogRecordProcessor(
            inner,
            options,
            maxExportBatchSize: 1024,
            scheduledDelayMilliseconds: 600000,
            maxQueueSize: 4096))
        using (var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddOpenTelemetry(logging => logging.AddProcessor(processor))))
        {
            var logger = loggerFactory.CreateLogger("cat");
            for (var i = 0; i < arrivals; i++)
            {
                logger.LogInformation("message");
            }

            processor.ForceFlush();
        }

        Assert.Equal(budget, captured.Count);
        Assert.All(captured, r => Assert.True(r.AdjustedCount > 0.0 && Numeric.IsFinite(r.AdjustedCount)));
    }

    [Fact]
    public void AdjustedCounts_RecoverArrivalCountAcrossWindows()
    {
        const int windows = 60;
        const int perWindow = 1000;
        var captured = new List<CapturedRecord>();
        var options = new BottomFloorLogSamplerOptions { Budget = 50 };

        using var inner = new CapturingExporter(captured);
        using (var processor = new BottomFloorLogRecordProcessor(
            inner,
            options,
            maxExportBatchSize: 4096,
            scheduledDelayMilliseconds: 600000,
            maxQueueSize: 8192))
        using (var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddOpenTelemetry(logging => logging.AddProcessor(processor))))
        {
            var logger = loggerFactory.CreateLogger("cat");
            for (var w = 0; w < windows; w++)
            {
                for (var i = 0; i < perWindow; i++)
                {
                    logger.LogInformation("message");
                }

                processor.ForceFlush();
            }
        }

        var totalAdjusted = captured.Sum(r => r.AdjustedCount);
        var expected = (double)windows * perWindow;
        var relativeError = Math.Abs(totalAdjusted - expected) / expected;
        Assert.True(relativeError < 0.05, $"relative error {relativeError:F4} exceeded 0.05 (adjusted {totalAdjusted}, expected {expected})");
    }

    [Fact]
    public void Constructor_RejectsBatchSizeNotAboveBudget()
    {
        // A window no larger than the budget is forwarded whole, so such a
        // configuration would silently never sample anything.
        using var inner = new CapturingExporter(new List<CapturedRecord>());
        var options = new BottomFloorLogSamplerOptions { Budget = 100 };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BottomFloorLogRecordProcessor(inner, options, maxExportBatchSize: 100));
    }

    [Fact]
    public void ForwardedRecords_KeepApplicationAttributesAndSeeTheParentProvider()
    {
        // Records are pooled and recycled as the batch is enumerated, so a
        // forwarded record must be a self-contained copy rather than a reused
        // buffer holding only the sampler's stamps. The inner exporter must also
        // observe the provider it would have seen undecorated.
        const int budget = 10;
        var probe = new ProbeExporter();
        var options = new BottomFloorLogSamplerOptions { Budget = budget };

        using (var processor = new BottomFloorLogRecordProcessor(
            probe,
            options,
            maxExportBatchSize: 1024,
            scheduledDelayMilliseconds: 600000,
            maxQueueSize: 4096))
        using (var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddOpenTelemetry(logging => logging.AddProcessor(processor))))
        {
            var logger = loggerFactory.CreateLogger("cat");
            for (var i = 0; i < 500; i++)
            {
#pragma warning disable CA1873
                logger.LogInformation("event in {Operation}", "checkout");
#pragma warning restore CA1873
            }

            processor.ForceFlush();
        }

        Assert.Equal(budget, probe.Operations.Count);
        Assert.All(probe.Operations, operation => Assert.Equal("checkout", operation));
        Assert.True(probe.SawParentProvider);
    }

    private sealed class ProbeExporter : BaseExporter<LogRecord>
    {
        public List<string?> Operations { get; } = new();

        public bool SawParentProvider { get; private set; }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            this.SawParentProvider |= this.ParentProvider != null;

            foreach (var record in batch)
            {
                string? operation = null;
                if (record.Attributes != null)
                {
                    foreach (var attribute in record.Attributes)
                    {
                        if (string.Equals(attribute.Key, "Operation", StringComparison.Ordinal))
                        {
                            operation = attribute.Value as string;
                        }
                    }
                }

                this.Operations.Add(operation);
            }

            return ExportResult.Success;
        }
    }
}
