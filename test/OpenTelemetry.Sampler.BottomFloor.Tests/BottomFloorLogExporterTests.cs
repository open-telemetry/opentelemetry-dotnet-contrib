// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Sampler.BottomFloor.Tests;

[Collection("LogPipeline")]
public class BottomFloorLogExporterTests
{
    [Fact]
    public void Constructor_RejectsNullInnerExporter()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BottomFloorLogExporter(null!, new BottomFloorLogSamplerOptions()));
    }

    [Fact]
    public void Constructor_RejectsNullOptions()
    {
        using var inner = new CapturingExporter(new List<CapturedRecord>());
        Assert.Throws<ArgumentNullException>(() => new BottomFloorLogExporter(inner, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveBudget(int budget)
    {
        using var inner = new CapturingExporter(new List<CapturedRecord>());
        var options = new BottomFloorLogSamplerOptions { Budget = budget };
        Assert.Throws<ArgumentOutOfRangeException>(() => new BottomFloorLogExporter(inner, options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_RejectsMissingAdjustedCountAttribute(string? attribute)
    {
        using var inner = new CapturingExporter(new List<CapturedRecord>());
        var options = new BottomFloorLogSamplerOptions { AdjustedCountAttribute = attribute! };
        Assert.Throws<ArgumentException>(() => new BottomFloorLogExporter(inner, options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_RejectsMissingSquaredCoefficientOfVariationAttribute(string? attribute)
    {
        using var inner = new CapturingExporter(new List<CapturedRecord>());
        var options = new BottomFloorLogSamplerOptions { SquaredCoefficientOfVariationAttribute = attribute! };
        Assert.Throws<ArgumentException>(() => new BottomFloorLogExporter(inner, options));
    }

    [Fact]
    public void EmptyWindow_SucceedsWithoutInvokingTheInnerExporter()
    {
        using var inner = new LifecycleExporter();
        using var exporter = new BottomFloorLogExporter(inner, new BottomFloorLogSamplerOptions { Budget = 4 });

        // A window with no arrivals must still close cleanly. Forwarding an empty
        // batch would make a downstream exporter do useless work, so the exporter
        // reports success without calling it at all.
        var result = exporter.Export(new Batch<LogRecord>(Array.Empty<LogRecord>(), 0));

        Assert.Equal(ExportResult.Success, result);
        Assert.Equal(0, inner.ExportCalls);
    }

    [Fact]
    public void ForceFlushAndShutdown_AreDelegatedToTheInnerExporter()
    {
        using var inner = new LifecycleExporter();
        using var exporter = new BottomFloorLogExporter(inner, new BottomFloorLogSamplerOptions { Budget = 4 });

        Assert.True(exporter.ForceFlush());
        Assert.Equal(1, inner.ForceFlushCalls);

        Assert.True(exporter.Shutdown());
        Assert.Equal(1, inner.ShutdownCalls);
    }

    [Fact]
    public void Retention_CanBindTheSdkCopyPrimitive()
    {
        // The sampler can only hold a record past its enumeration visit by
        // copying it through the SDK's internal LogRecord.Copy(). If a future SDK
        // removes or renames that member the component degrades to forwarding
        // everything unsampled, which is safe but silent, so assert the binding
        // directly instead of relying on a budget test to notice.
        Assert.True(LogRecordRetention.CanClone);
    }

    [Fact]
    public void UnderFullWindow_OmitsTheAdjustedCountForFullyIncludedRecords()
    {
        var captured = new List<CapturedRecord>();
        RunSingleWindow(
            captured,
            budget: 100,
            seed: 1,
            arrivals: Enumerable.Repeat("cat", 10).ToArray());

        Assert.Equal(10, captured.Count);

        // A window no larger than the budget keeps every record with an inclusion
        // probability of one, so the adjusted count is exactly one and omitted;
        // the variance companion is omitted with it.
        Assert.All(captured, r => Assert.True(double.IsNaN(r.AdjustedCount)));
        Assert.All(captured, r => Assert.True(double.IsNaN(r.SquaredCoefficientOfVariation)));
    }

    [Fact]
    public void FullWindow_ForwardsExactlyBudgetRecords()
    {
        var captured = new List<CapturedRecord>();
        RunSingleWindow(
            captured,
            budget: 10,
            seed: 2,
            arrivals: Enumerable.Repeat("cat", 500).ToArray());

        Assert.Equal(10, captured.Count);
        Assert.All(captured, r => Assert.True(r.AdjustedCount > 0.0 && double.IsFinite(r.AdjustedCount)));
        Assert.All(captured, r => Assert.True(r.SquaredCoefficientOfVariation >= 0.0));
    }

    [Fact]
    public void FullWindow_SpreadsBudgetAcrossCallsites()
    {
        var arrivals = new List<string>();
        arrivals.AddRange(Enumerable.Repeat("a", 500));
        arrivals.AddRange(Enumerable.Repeat("b", 500));

        var captured = new List<CapturedRecord>();
        RunSingleWindow(captured, budget: 10, seed: 3, arrivals: arrivals.ToArray());

        Assert.Equal(10, captured.Count);
        var categories = captured.Select(r => r.CategoryName).Distinct().ToList();
        Assert.Equal(2, categories.Count);
    }

    [Fact]
    public void CustomAttributeNames_AreUsed()
    {
        var captured = new List<CapturedRecord>();
        var options = new BottomFloorLogSamplerOptions
        {
            Budget = 2,
            AdjustedCountAttribute = "sampling.count",
            SquaredCoefficientOfVariationAttribute = "sampling.variance",
        };

        var keys = new HashSet<string>();
        using (var inner = new CapturingExporter(captured, keys))
        using (var loggerFactory = BuildLoggerFactory(inner, options, seed: 4, out var processor, arrivals: 5))
        {
            var logger = loggerFactory.CreateLogger("cat");
            for (var i = 0; i < 5; i++)
            {
                logger.LogInformation("message");
            }

            processor.ForceFlush();
        }

        Assert.Contains("sampling.count", keys);
        Assert.Contains("sampling.variance", keys);
        Assert.DoesNotContain("otel.logs.adjusted_count", keys);
    }

    [Fact]
    public void AdjustedCounts_RecoverArrivalCountAcrossWindows()
    {
        const int windows = 60;
        const int perWindow = 1000;
        var captured = new List<CapturedRecord>();
        var options = new BottomFloorLogSamplerOptions { Budget = 50 };

        using (var inner = new CapturingExporter(captured))
        using (var loggerFactory = BuildLoggerFactory(inner, options, seed: 5, out var processor, arrivals: perWindow))
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

    private static void RunSingleWindow(List<CapturedRecord> captured, int budget, int seed, string[] arrivals)
    {
        var options = new BottomFloorLogSamplerOptions { Budget = budget };

        using var inner = new CapturingExporter(captured);
        using var loggerFactory = BuildLoggerFactory(inner, options, seed, out var processor, arrivals: arrivals.Length);

        var loggers = new Dictionary<string, ILogger>();
        foreach (var category in arrivals)
        {
            if (!loggers.TryGetValue(category, out var logger))
            {
                logger = loggerFactory.CreateLogger(category);
                loggers[category] = logger;
            }

            logger.LogInformation("message");
        }

        processor.ForceFlush();
    }

    private static ILoggerFactory BuildLoggerFactory(
        BaseExporter<LogRecord> inner,
        BottomFloorLogSamplerOptions options,
        int seed,
        out BatchLogRecordExportProcessor processor,
        int arrivals)
    {
        // One export batch is one sampling window, so the batch must be able to
        // hold every arrival: size the queue and batch above the arrival count.
        var capacity = Math.Max(2048, (arrivals * 2) + 16);
        var batchSize = Math.Max(512, arrivals + 8);
        var exporter = new BottomFloorLogExporter(inner, options, new Random(seed));
        processor = new BatchLogRecordExportProcessor(
            exporter,
            maxQueueSize: capacity,
            scheduledDelayMilliseconds: 600000,
            exporterTimeoutMilliseconds: 30000,
            maxExportBatchSize: batchSize);

        var capturedProcessor = processor;
        return LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(logging =>
            {
                logging.AddProcessor(capturedProcessor);
            });
        });
    }

    internal sealed class LifecycleExporter : BaseExporter<LogRecord>
    {
        public int ExportCalls { get; private set; }

        public int ForceFlushCalls { get; private set; }

        public int ShutdownCalls { get; private set; }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            this.ExportCalls++;
            return ExportResult.Success;
        }

        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            this.ForceFlushCalls++;
            return true;
        }

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            this.ShutdownCalls++;
            return true;
        }
    }

    internal sealed class CapturedRecord
    {
        public CapturedRecord(string? categoryName, int eventId, double adjustedCount, double squaredCoefficientOfVariation)
        {
            this.CategoryName = categoryName;
            this.EventId = eventId;
            this.AdjustedCount = adjustedCount;
            this.SquaredCoefficientOfVariation = squaredCoefficientOfVariation;
        }

        public string? CategoryName { get; }

        public int EventId { get; }

        public double AdjustedCount { get; }

        public double SquaredCoefficientOfVariation { get; }
    }

    internal sealed class CapturingExporter : BaseExporter<LogRecord>
    {
        private readonly List<CapturedRecord> captured;
        private readonly HashSet<string>? observedKeys;

        public CapturingExporter(List<CapturedRecord> captured, HashSet<string>? observedKeys = null)
        {
            this.captured = captured;
            this.observedKeys = observedKeys;
        }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var record in batch)
            {
                var adjusted = double.NaN;
                var cv2 = double.NaN;
                if (record.Attributes != null)
                {
                    foreach (var attribute in record.Attributes)
                    {
                        this.observedKeys?.Add(attribute.Key);
                        if (attribute.Value is double value)
                        {
                            if (attribute.Key.EndsWith("adjusted_count", StringComparison.Ordinal) ||
                                attribute.Key.EndsWith("count", StringComparison.Ordinal))
                            {
                                adjusted = value;
                            }
                            else if (attribute.Key.EndsWith("cv2", StringComparison.Ordinal) ||
                                attribute.Key.EndsWith("variance", StringComparison.Ordinal))
                            {
                                cv2 = value;
                            }
                        }
                    }
                }

                this.captured.Add(new CapturedRecord(record.CategoryName, record.EventId.Id, adjusted, cv2));
            }

            return ExportResult.Success;
        }
    }
}
