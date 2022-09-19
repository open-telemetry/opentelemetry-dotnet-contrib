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
| LoggerWithMessageTemplate |                   False | 979.5 ns | 11.46 ns | 10.72 ns | 0.0401 |     256 B |
| LoggerWithDirectLoggerAPI |                   False | 887.9 ns | 17.24 ns | 16.13 ns | 0.0620 |     392 B |
| LoggerWithSourceGenerator |                   False | 965.8 ns | 16.84 ns | 15.75 ns | 0.0343 |     216 B |
|        SerializeLogRecord |                   False | 696.5 ns | 13.94 ns | 14.92 ns | 0.0038 |      24 B |
|                    Export |                   False | 744.9 ns | 12.91 ns | 12.08 ns | 0.0038 |      24 B |
| LoggerWithMessageTemplate |                    True | 978.6 ns | 18.95 ns | 19.46 ns | 0.0401 |     256 B |
| LoggerWithDirectLoggerAPI |                    True | 878.3 ns | 11.43 ns | 10.69 ns | 0.0620 |     392 B |
| LoggerWithSourceGenerator |                    True | 942.8 ns | 14.55 ns | 13.61 ns | 0.0343 |     216 B |
|        SerializeLogRecord |                    True | 707.3 ns |  9.01 ns |  8.42 ns | 0.0038 |      24 B |
|                    Export |                    True | 752.0 ns |  8.97 ns |  7.49 ns | 0.0038 |      24 B |


With Scopes (https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/545)

|                    Method | IncludeFormattedMessage |       Mean |    Error |   StdDev |     Median |  Gen 0 | Allocated |
|-------------------------- |------------------------ |-----------:|---------:|---------:|-----------:|-------:|----------:|
| LoggerWithMessageTemplate |                   False | 1,042.8 ns | 19.34 ns | 54.55 ns | 1,022.1 ns | 0.0572 |     360 B |
| LoggerWithDirectLoggerAPI |                   False |   953.4 ns | 13.90 ns | 13.00 ns |   950.4 ns | 0.0782 |     496 B |
| LoggerWithSourceGenerator |                   False |   962.1 ns | 18.93 ns | 17.71 ns |   957.6 ns | 0.0496 |     320 B |
|        SerializeLogRecord |                   False |   722.8 ns |  6.26 ns |  5.23 ns |   722.9 ns | 0.0200 |     128 B |
|                    Export |                   False |   789.2 ns | 15.11 ns | 14.14 ns |   787.3 ns | 0.0200 |     128 B |
| LoggerWithMessageTemplate |                    True |   986.8 ns | 12.56 ns | 11.13 ns |   983.4 ns | 0.0572 |     360 B |
| LoggerWithDirectLoggerAPI |                    True |   932.1 ns | 18.25 ns | 20.29 ns |   924.7 ns | 0.0782 |     496 B |
| LoggerWithSourceGenerator |                    True |   980.0 ns | 15.56 ns | 14.55 ns |   979.6 ns | 0.0496 |     320 B |
|        SerializeLogRecord |                    True |   737.5 ns | 13.46 ns | 12.59 ns |   738.8 ns | 0.0200 |     128 B |
|                    Export |                    True |   772.2 ns | 14.02 ns | 13.11 ns |   774.8 ns | 0.0200 |     128 B |
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
                });

                loggerOptions.IncludeFormattedMessage = this.IncludeFormattedMessage;
            }));

        this.logger = this.loggerFactory.CreateLogger("TestLogger");

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
