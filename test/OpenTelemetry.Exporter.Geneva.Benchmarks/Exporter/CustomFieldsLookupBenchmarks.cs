// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

/*
Compares the cost of MsgPack log serialization when custom fields are configured
globally (the pre-existing behavior, "before") versus per table (the new
CustomFieldsMappings feature, "after") for 10 tables each carrying 20 custom
fields. The per-table path is expected to be slightly slower because it performs
a dictionary lookup keyed by the final table name on the hot path, whereas the
global path returns the single shared field set directly.

Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 16 logical and 8 physical cores

.NET 10.0.8 (zero-alloc byte-span alternate lookup):
| Method                                  | Mean     | Ratio | Allocated |
|---------------------------------------- |---------:|------:|----------:|
| SerializeLogRecord_GlobalCustomFields   | 293.5 ns |  1.00 |         - |
| SerializeLogRecord_PerTableCustomFields | 319.6 ns |  1.09 |         - |

.NET 8.0.27 (key-construction fallback, no ReadOnlySpan<byte> alternate lookup):
| Method                                  | Mean     | Ratio | Allocated |
|---------------------------------------- |---------:|------:|----------:|
| SerializeLogRecord_GlobalCustomFields   | 380.2 ns |  1.00 |         - |
| SerializeLogRecord_PerTableCustomFields | 409.4 ns |  1.08 |      40 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmarks;

#pragma warning disable CA1873 // Avoid potentially expensive logging

[MemoryDiagnoser]
public class CustomFieldsLookupBenchmarks
{
    private const int TableCount = 10;
    private const int FieldsPerTable = 20;

    private readonly MsgPackLogExporter globalExporter;
    private readonly MsgPackLogExporter perTableExporter;
    private readonly LogRecord logRecord;

    public CustomFieldsLookupBenchmarks()
    {
        // The 20 custom fields applied to the record. Two of them match the
        // structured-logging template attributes so the serialized output is
        // identical for both exporters; only the resolution mechanism differs.
        var fields = new List<string> { "Food", "Price" };
        for (var i = fields.Count; i < FieldsPerTable; i++)
        {
            fields.Add("Field" + i);
        }

        // "Before": a single global CustomFields set shared by every table.
        this.globalExporter = new MsgPackLogExporter(
            new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                CustomFields = fields.ToArray(),
            },
            () => Resource.Empty);

        // "After": 10 tables, each with its own 20 custom fields. The benchmark
        // record maps to BenchTable5; the other nine entries simply make the
        // lookup non-trivial.
        var tableMappings = new Dictionary<string, string>();
        var customFieldsMappings = new Dictionary<string, IEnumerable<string>>();
        for (var t = 0; t < TableCount; t++)
        {
            var table = "BenchTable" + t;
            tableMappings["BenchCategory" + t] = table;
            customFieldsMappings[table] = fields.ToArray();
        }

        this.perTableExporter = new MsgPackLogExporter(
            new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                TableNameMappings = tableMappings,
                CustomFieldsMappings = customFieldsMappings,
            },
            () => Resource.Empty);

        this.logRecord = GenerateTestLogRecord("BenchCategory5");
    }

    [Benchmark(Baseline = true)]
    public void SerializeLogRecord_GlobalCustomFields()
        => this.globalExporter.SerializeLogRecord(this.logRecord);

    [Benchmark]
    public void SerializeLogRecord_PerTableCustomFields()
        => this.perTableExporter.SerializeLogRecord(this.logRecord);

    [GlobalCleanup]
    public void Cleanup()
    {
        this.globalExporter.Dispose();
        this.perTableExporter.Dispose();
    }

    private static LogRecord GenerateTestLogRecord(string categoryName)
    {
        var items = new List<LogRecord>(1);
        using var factory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddInMemoryExporter(items);
            }));

        var logger = factory.CreateLogger(categoryName);
        logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);
        return items[0];
    }
}
