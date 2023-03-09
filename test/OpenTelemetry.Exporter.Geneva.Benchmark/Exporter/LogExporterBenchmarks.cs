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
BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.963)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2


|                    Method | IncludeFormattedMessage |       Mean |    Error |   StdDev |   Gen0 | Allocated |
|-------------------------- |------------------------ |-----------:|---------:|---------:|-------:|----------:|
| LoggerWithMessageTemplate |                   False | 1,221.9 ns | 17.52 ns | 15.53 ns | 0.0153 |     104 B |
| LoggerWithDirectLoggerAPI |                   False | 1,109.6 ns | 22.14 ns | 34.47 ns | 0.0381 |     240 B |
| LoggerWithSourceGenerator |                   False | 1,117.7 ns |  9.94 ns |  7.76 ns | 0.0095 |      64 B |
|        SerializeLogRecord |                   False |   560.0 ns |  2.87 ns |  2.40 ns |      - |         - |
|                    Export |                   False |   891.0 ns | 17.06 ns | 32.05 ns |      - |         - |
| LoggerWithMessageTemplate |                    True | 1,243.7 ns | 24.79 ns | 35.55 ns | 0.0153 |     104 B |
| LoggerWithDirectLoggerAPI |                    True | 1,090.8 ns | 12.85 ns | 10.04 ns | 0.0381 |     240 B |
| LoggerWithSourceGenerator |                    True | 1,186.1 ns | 23.58 ns | 45.99 ns | 0.0095 |      64 B |
|        SerializeLogRecord |                    True |   564.8 ns |  5.20 ns |  4.06 ns |      - |         - |
|                    Export |                    True |   874.5 ns | 17.38 ns | 24.37 ns |      - |         - |
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
        this.logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);
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
        logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);
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
