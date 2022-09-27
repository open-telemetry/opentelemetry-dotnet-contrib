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
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22621
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100-preview.6.22352.1
  [Host]     : .NET 6.0.9 (6.0.922.41905), X64 RyuJIT
  DefaultJob : .NET 6.0.9 (6.0.922.41905), X64 RyuJIT

Without Scopes

|                    Method | IncludeFormattedMessage |       Mean |   Error |  StdDev |  Gen 0 | Allocated |
|-------------------------- |------------------------ |-----------:|--------:|--------:|-------:|----------:|
| LoggerWithMessageTemplate |                   False | 1,273.1 ns | 6.09 ns | 5.39 ns | 0.0362 |     232 B |
| LoggerWithDirectLoggerAPI |                   False | 1,213.0 ns | 9.71 ns | 8.61 ns | 0.0572 |     368 B |
| LoggerWithSourceGenerator |                   False | 1,243.5 ns | 6.13 ns | 5.44 ns | 0.0305 |     192 B |
|        SerializeLogRecord |                   False |   587.7 ns | 2.71 ns | 2.54 ns |      - |         - |
|                    Export |                   False |   955.0 ns | 5.46 ns | 5.11 ns |      - |         - |
| LoggerWithMessageTemplate |                    True | 1,261.1 ns | 6.59 ns | 5.84 ns | 0.0362 |     232 B |
| LoggerWithDirectLoggerAPI |                    True | 1,214.4 ns | 4.56 ns | 4.27 ns | 0.0572 |     368 B |
| LoggerWithSourceGenerator |                    True | 1,229.6 ns | 6.84 ns | 6.40 ns | 0.0305 |     192 B |
|        SerializeLogRecord |                    True |   581.6 ns | 2.38 ns | 2.11 ns |      - |         - |
|                    Export |                    True |   958.4 ns | 3.02 ns | 2.52 ns |      - |         - |


With Scopes (https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/545)

|                    Method | IncludeFormattedMessage |       Mean |   Error |  StdDev |  Gen 0 | Allocated |
|-------------------------- |------------------------ |-----------:|--------:|--------:|-------:|----------:|
| LoggerWithMessageTemplate |                   False | 1,280.8 ns | 7.45 ns | 6.61 ns | 0.0534 |     336 B |
| LoggerWithDirectLoggerAPI |                   False | 1,261.5 ns | 6.38 ns | 5.96 ns | 0.0744 |     472 B |
| LoggerWithSourceGenerator |                   False | 1,309.3 ns | 4.83 ns | 4.52 ns | 0.0458 |     296 B |
|        SerializeLogRecord |                   False |   611.3 ns | 4.63 ns | 4.11 ns | 0.0162 |     104 B |
|                    Export |                   False | 1,012.2 ns | 7.56 ns | 7.07 ns | 0.0153 |     104 B |
| LoggerWithMessageTemplate |                    True | 1,278.3 ns | 6.63 ns | 5.88 ns | 0.0534 |     336 B |
| LoggerWithDirectLoggerAPI |                    True | 1,263.8 ns | 8.26 ns | 7.73 ns | 0.0744 |     472 B |
| LoggerWithSourceGenerator |                    True | 1,273.4 ns | 5.57 ns | 5.21 ns | 0.0458 |     296 B |
|        SerializeLogRecord |                    True |   604.3 ns | 2.83 ns | 2.65 ns | 0.0162 |     104 B |
|                    Export |                    True | 1,003.6 ns | 9.29 ns | 8.69 ns | 0.0153 |     104 B |
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
