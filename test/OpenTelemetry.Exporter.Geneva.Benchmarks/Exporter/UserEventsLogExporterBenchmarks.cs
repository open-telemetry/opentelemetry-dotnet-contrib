// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Geneva.EventHeader;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet v0.13.12, Ubuntu 24.04.1 LTS (Noble Numbat) WSL
AMD EPYC 7763, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.103
  [Host]     : .NET 8.0.12 (8.0.1224.60305), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.12 (8.0.1224.60305), X64 RyuJIT AVX2


| Method                        | Mean          | Error       | StdDev      | Gen0   | Allocated |
|------------------------------ |--------------:|------------:|------------:|-------:|----------:|
| MsgPack_SerializeLogRecord    |    440.674 ns |   3.1138 ns |   2.7603 ns |      - |         - |
| UserEvents_SerializeLogRecord |    641.591 ns |   3.9530 ns |   3.3010 ns | 0.0057 |     104 B |
| MsgPack_ExportLogRecord       | 52,815.968 ns | 396.9791 ns | 351.9117 ns | 0.0610 |    1552 B |
| UserEvents_ExportLogRecord    |      2.024 ns |   0.0472 ns |   0.0394 ns |      - |         - |

$ uname -r
6.6.36.3-microsoft-standard-WSL2
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmarks;

[MemoryDiagnoser]
public class UserEventsLogExporterBenchmarks
{
    private readonly LogRecord logRecord;
    private readonly Batch<LogRecord> batch;
    private readonly MsgPackLogExporter msgPackExporter;
    private readonly EventHeaderLogExporter eventHeaderExporter;
    private readonly ILoggerFactory loggerFactoryForMsgPack;

    public UserEventsLogExporterBenchmarks()
    {
        this.msgPackExporter = new MsgPackLogExporter(new GenevaExporterOptions
        {
            ConnectionString = "Endpoint=unix:/var/run/mdsd/default_fluent.socket",
            PrepopulatedFields = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        });

        this.eventHeaderExporter = new EventHeaderLogExporter(new GenevaExporterOptions()
        {
            ConnectionString = "PrivatePreviewEnableUserEvents=true",
            PrepopulatedFields = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        });

        this.logRecord = GenerateTestLogRecord();
        this.batch = GenerateTestLogRecordBatch();

        this.loggerFactoryForMsgPack = LoggerFactory.Create(builder =>
            builder.AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddGenevaLogExporter(exporterOptions =>
                {
                    exporterOptions.ConnectionString = "Endpoint=unix:/var/run/mdsd/default_fluent.socket";
                    exporterOptions.PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    };
                });
            }));
    }

    [Benchmark]
    public void MsgPack_SerializeLogRecord()
    {
        this.msgPackExporter.SerializeLogRecord(this.logRecord);
    }

    [Benchmark]
    public void UserEvents_SerializeLogRecord()
    {
        this.eventHeaderExporter.SerializeLogRecord(this.logRecord);
    }

    [Benchmark]
    public void MsgPack_ExportLogRecord()
    {
        this.msgPackExporter.Export(this.batch);
    }

    [Benchmark]
    public void UserEvents_ExportLogRecord()
    {
        this.eventHeaderExporter.Export(this.batch);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.batch.Dispose();
        this.loggerFactoryForMsgPack.Dispose();
        this.eventHeaderExporter.Dispose();
        this.msgPackExporter.Dispose();
    }

    private static LogRecord GenerateTestLogRecord()
    {
        var exportedItems = new List<LogRecord>(1);
        using var factory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddInMemoryExporter(exportedItems);
            }));

        var logger = factory.CreateLogger("TestLogger");
        logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);
        return exportedItems[0];
    }

    private static Batch<LogRecord> GenerateTestLogRecordBatch()
    {
        var items = new List<LogRecord>(1);
        using var batchGeneratorExporter = new BatchGeneratorExporter();
        using var factory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(batchGeneratorExporter));
            }));

        var logger = factory.CreateLogger("TestLogger");
        logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);
        return batchGeneratorExporter.Batch;
    }

    private class BatchGeneratorExporter : BaseExporter<LogRecord>
    {
        public Batch<LogRecord> Batch { get; set; }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            this.Batch = batch;
            return ExportResult.Success;
        }
    }
}

#endif
