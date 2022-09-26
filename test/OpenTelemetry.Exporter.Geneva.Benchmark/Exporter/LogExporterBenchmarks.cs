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
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100-preview.6.22352.1
  [Host]     : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT
  DefaultJob : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT

Without Scopes

|                    Method | IncludeFormattedMessage |     Mean |    Error |   StdDev |  Gen 0 | Allocated |
|-------------------------- |------------------------ |---------:|---------:|---------:|-------:|----------:|
| LoggerWithMessageTemplate |                   False | 823.3 ns |  2.20 ns |  1.84 ns | 0.0362 |     232 B |
| LoggerWithDirectLoggerAPI |                   False | 749.2 ns |  5.49 ns |  4.87 ns | 0.0582 |     368 B |
| LoggerWithSourceGenerator |                   False | 798.4 ns |  2.72 ns |  2.41 ns | 0.0305 |     192 B |
|        SerializeLogRecord |                   False | 596.3 ns | 10.69 ns | 10.00 ns |      - |         - |
|                    Export |                   False | 655.1 ns | 12.75 ns | 15.18 ns |      - |         - |
| LoggerWithMessageTemplate |                    True | 817.8 ns |  3.04 ns |  2.85 ns | 0.0362 |     232 B |
| LoggerWithDirectLoggerAPI |                    True | 750.5 ns |  4.73 ns |  4.43 ns | 0.0582 |     368 B |
| LoggerWithSourceGenerator |                    True | 782.3 ns |  9.26 ns |  8.67 ns | 0.0305 |     192 B |
|        SerializeLogRecord |                    True | 580.3 ns |  3.39 ns |  3.17 ns |      - |         - |
|                    Export |                    True | 640.1 ns |  4.26 ns |  3.98 ns |      - |         - |


With Scopes (https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/545)

|                    Method | IncludeFormattedMessage |     Mean |    Error |   StdDev |  Gen 0 | Allocated |
|-------------------------- |------------------------ |---------:|---------:|---------:|-------:|----------:|
| LoggerWithMessageTemplate |                   False | 872.5 ns |  5.37 ns |  4.48 ns | 0.0534 |     336 B |
| LoggerWithDirectLoggerAPI |                   False | 808.6 ns | 12.32 ns | 10.92 ns | 0.0744 |     472 B |
| LoggerWithSourceGenerator |                   False | 828.1 ns |  4.06 ns |  3.80 ns | 0.0467 |     296 B |
|        SerializeLogRecord |                   False | 607.4 ns |  1.69 ns |  1.50 ns | 0.0162 |     104 B |
|                    Export |                   False | 658.8 ns |  2.20 ns |  2.05 ns | 0.0162 |     104 B |
| LoggerWithMessageTemplate |                    True | 845.7 ns |  3.77 ns |  3.52 ns | 0.0534 |     336 B |
| LoggerWithDirectLoggerAPI |                    True | 803.4 ns |  5.37 ns |  5.02 ns | 0.0744 |     472 B |
| LoggerWithSourceGenerator |                    True | 836.9 ns |  7.04 ns |  6.24 ns | 0.0467 |     296 B |
|        SerializeLogRecord |                    True | 605.5 ns |  3.30 ns |  3.09 ns | 0.0162 |     104 B |
|                    Export |                    True | 664.1 ns |  2.01 ns |  1.88 ns | 0.0162 |     104 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark;

[MemoryDiagnoser]
public class LogExporterBenchmarks
{
    private readonly ILogger logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly GenevaLogExporter exporter;
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
                });

                loggerOptions.IncludeFormattedMessage = this.IncludeFormattedMessage;
            }));

        this.logger = this.loggerFactory.CreateLogger("TestLogger");

        // For msgpack serialization + export
        this.logRecord = GenerateTestLogRecord();
        this.batch = GenerateTestLogRecordBatch();
        this.exporter = new GenevaLogExporter(new GenevaExporterOptions
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
