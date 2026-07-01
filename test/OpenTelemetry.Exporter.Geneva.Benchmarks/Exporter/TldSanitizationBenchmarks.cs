// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Geneva.Tld;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.Geneva.Benchmarks;

#pragma warning disable CA1873 // Avoid potentially expensive logging

// This benchmark isolates the cost of category-name handling in the TLD log
// exporter when running in pass-through table-mapping mode ("*" -> "*").
//
// In that mode TldLogExporter calls GetSanitizedCategoryName(categoryName) for
// every single log record. The method allocates a brand new string on each
// call (Span<char>.Slice(...).ToString()). Because the set of logger category
// names in a process is small and stable, the sanitized value could be cached
// the way MsgPack's TableNameSerializer already does, turning a per-event
// sanitize+allocate into a dictionary lookup.
[MemoryDiagnoser]
public class TldSanitizationBenchmarks
{
    private TldLogExporter passthroughExporter = null!;
    private CategorySanitizerProbe sanitizerProbe = null!;
    private LogRecord logRecord = null!;
    private Batch<LogRecord> batch;

    // "AlreadyNormalized" exercises the accelerated fast path that returns the
    // category name verbatim. "NeedsNormalization" (contains '.') exercises the
    // sanitize + copy-on-write cache path.
    [Params("TestLogger", "TestCompany.TestNamespace.TestLogger")]
    public string CategoryName { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        // "*" -> "*" turns on shouldPassThruTableMappings, which is the path
        // that sanitizes the category name on every record.
        this.passthroughExporter = new TldLogExporter(new GenevaExporterOptions()
        {
            ConnectionString = "EtwSession=OpenTelemetry;PrivatePreviewEnableTraceLoggingDynamic=true",
            TableNameMappings = new Dictionary<string, string>
            {
                ["*"] = "*",
            },
            PrepopulatedFields = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        });

        this.sanitizerProbe = new CategorySanitizerProbe(new GenevaExporterOptions()
        {
            ConnectionString = "EtwSession=OpenTelemetry;PrivatePreviewEnableTraceLoggingDynamic=true",
            TableNameMappings = new Dictionary<string, string>
            {
                ["*"] = "*",
            },
        });

        this.logRecord = GenerateTestLogRecord(this.CategoryName);
        this.batch = GenerateTestLogRecordBatch(this.CategoryName);
    }

    // Isolates just the category-name handling (the rest of serialization adds
    // ~350 ns of fixed cost that hides the difference at the end-to-end level).
    [Benchmark]
    public string TLD_GetSanitizedCategoryName()
        => this.sanitizerProbe.Sanitize(this.CategoryName);

    [Benchmark]
    public void TLD_SerializeLogRecord_Passthrough()
        => this.passthroughExporter.SerializeLogRecord(this.logRecord);

    [Benchmark]
    public void TLD_ExportLogRecord_Passthrough()
        => this.passthroughExporter.Export(this.batch);

    [GlobalCleanup]
    public void Cleanup()
    {
        this.batch.Dispose();
        this.passthroughExporter.Dispose();
        this.sanitizerProbe.Dispose();
    }

    private static LogRecord GenerateTestLogRecord(string categoryName)
    {
        var exportedItems = new List<LogRecord>(1);
        using var factory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddInMemoryExporter(exportedItems);
            }));

        var logger = factory.CreateLogger(categoryName);
        logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);
        return exportedItems[0];
    }

    private static Batch<LogRecord> GenerateTestLogRecordBatch(string categoryName)
    {
        var items = new List<LogRecord>(1);
        using var batchGeneratorExporter = new BatchGeneratorExporter();
        using var factory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(batchGeneratorExporter));
            }));

        var logger = factory.CreateLogger(categoryName);
        logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);
        return batchGeneratorExporter.Batch;
    }

    private sealed class BatchGeneratorExporter : BaseExporter<LogRecord>
    {
        public Batch<LogRecord> Batch { get; set; }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            this.Batch = batch;
            return ExportResult.Success;
        }
    }

    // Minimal TldLogCommon subclass that exposes the protected sanitization entry
    // point so the cache + fast-path can be benchmarked in isolation.
    private sealed class CategorySanitizerProbe : TldLogCommon
    {
        public CategorySanitizerProbe(GenevaExporterOptions options)
            : base(options)
        {
        }

        public string Sanitize(string categoryName)
            => this.GetSanitizedCategoryName(categoryName);
    }
}
