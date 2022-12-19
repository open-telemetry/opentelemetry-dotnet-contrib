// <copyright file="LogExporterBenchmarks.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

/*
BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.819)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100
  [Host]     : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.11 (6.0.1122.52304), X64 RyuJIT AVX2


|                    Method | IncludeFormattedMessage |     Mean |   Error |  StdDev |   Gen0 | Allocated |
|-------------------------- |------------------------ |---------:|--------:|--------:|-------:|----------:|
| LoggerWithMessageTemplate |                   False | 832.5 ns | 5.99 ns | 5.00 ns | 0.0162 |     104 B |
| LoggerWithDirectLoggerAPI |                   False | 766.2 ns | 3.85 ns | 3.60 ns | 0.0381 |     240 B |
| LoggerWithSourceGenerator |                   False | 815.3 ns | 2.89 ns | 2.41 ns | 0.0095 |      64 B |
|        SerializeLogRecord |                   False | 582.3 ns | 0.81 ns | 0.72 ns |      - |         - |
|                    Export |                   False | 646.0 ns | 1.10 ns | 0.86 ns |      - |         - |
| LoggerWithMessageTemplate |                    True | 847.7 ns | 5.56 ns | 5.20 ns | 0.0162 |     104 B |
| LoggerWithDirectLoggerAPI |                    True | 762.5 ns | 2.72 ns | 2.41 ns | 0.0381 |     240 B |
| LoggerWithSourceGenerator |                    True | 816.6 ns | 2.79 ns | 2.47 ns | 0.0095 |      64 B |
|        SerializeLogRecord |                    True | 586.3 ns | 1.80 ns | 1.69 ns |      - |         - |
|                    Export |                    True | 659.5 ns | 6.00 ns | 5.61 ns |      - |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark;

[MemoryDiagnoser]
public class LogExporterBenchmarks
{
    private readonly ILogger logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly MsgPackLogExporter exporter;
    private readonly LogRecord logRecord;
    private readonly Batch<LogRecord> batch;

    [Params(true, false)]
    public bool IncludeFormattedMessage { get; set; }

    public LogExporterBenchmarks()
    {
        // for total cost of logging + msgpack serialization + export
        this.loggerFactory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddGenevaLogExporter(exporterOptions =>
                {
                    exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
                    exporterOptions.PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    };
                    exporterOptions.TableNameMappings = new Dictionary<string, string>
                    {
                        ["*"] = "*",
                        ["TestCompany"] = "*",
                        ["TestCompany.TestNamespace"] = "*",
                        ["TestCompany.TestNamespace.TestLogger"] = "TestLoggerTable",
                    };
                });

                loggerOptions.IncludeFormattedMessage = this.IncludeFormattedMessage;
            }));

        this.logger = this.loggerFactory.CreateLogger("TestCompany.TestNamespace.TestLogger");

        // For msgpack serialization + export
        this.logRecord = GenerateTestLogRecord();
        this.batch = GenerateTestLogRecordBatch();
        this.exporter = new MsgPackLogExporter(new GenevaExporterOptions
        {
            ConnectionString = "EtwSession=OpenTelemetry",
            PrepopulatedFields = new Dictionary<string, object>
            {
                ["cloud.role"] = "BusyWorker",
                ["cloud.roleInstance"] = "CY1SCH030021417",
                ["cloud.roleVer"] = "9.0.15289.2",
            },
        });
    }

    [Benchmark]
    public void LoggerWithMessageTemplate()
    {
        this.logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
    }

    [Benchmark]
    public void LoggerWithDirectLoggerAPI()
    {
        var food = "artichoke";
        var price = 3.99;
        this.logger.Log(
            logLevel: LogLevel.Information,
            eventId: default,
            state: new List<KeyValuePair<string, object>>()
            {
                new KeyValuePair<string, object>("food", food),
                new KeyValuePair<string, object>("price", price),
            },
            exception: null,
            formatter: (state, ex) => $"Hello from {food} {price}.");
    }

    [Benchmark]
    public void LoggerWithSourceGenerator()
    {
        Food.SayHello(this.logger, "artichoke", 3.99);
    }

    [Benchmark]
    public void SerializeLogRecord()
    {
        this.exporter.SerializeLogRecord(this.logRecord);
    }

    [Benchmark]
    public void Export()
    {
        this.exporter.Export(this.batch);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.loggerFactory.Dispose();
        this.batch.Dispose();
        this.exporter.Dispose();
    }

    private static LogRecord GenerateTestLogRecord()
    {
        var items = new List<LogRecord>(1);
        using var factory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddInMemoryExporter(items);
            }));

        var logger = factory.CreateLogger("TestLogger");
        logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
        return items[0];
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
        logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
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
