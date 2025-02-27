// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Geneva.EventHeader;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Logs;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

/*
See UnixUserEventsDataTransportTests.cs for prerequisites for running user_events code.

$ uname -r
6.6.36.3-microsoft-standard-WSL2

BenchmarkDotNet v0.13.12, Ubuntu 24.04.1 LTS (Noble Numbat) WSL
AMD EPYC 7763, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.103
  [Host]     : .NET 8.0.12 (8.0.1224.60305), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.12 (8.0.1224.60305), X64 RyuJIT AVX2


| Method                        | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------------ |------------:|----------:|----------:|-------:|----------:|
| MsgPack_SerializeLogRecord    |    444.3 ns |   8.09 ns |  14.59 ns |      - |         - |
| UserEvents_SerializeLogRecord |    650.0 ns |   4.99 ns |   4.67 ns | 0.0057 |     104 B |
| MsgPack_ExportLogRecord       | 53,020.2 ns | 590.60 ns | 552.45 ns | 0.0610 |    1552 B |
| UserEvents_ExportLogRecord    |  3,489.2 ns |  69.04 ns |  64.58 ns | 0.0038 |     104 B |
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
    private readonly PerfTracepointListener perfTracepointListener;

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

        var perfTracepointListener = new PerfTracepointListener(
            "MicrosoftOpenTelemetryLogs_L4K1",
            MetricUnixUserEventsDataTransport.MetricsTracepointNameArgs);

        perfTracepointListener.Enable();

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

        try
        {
            this.perfTracepointListener.Disable();
        }
        catch
        {
        }

        this.perfTracepointListener.Dispose();
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
