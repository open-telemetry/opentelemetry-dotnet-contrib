// <copyright file="TLDLogExporterBenchmarks.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

/*
BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.521)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.100-preview.6.22352.1
  [Host]     : .NET 6.0.9 (6.0.922.41905), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.9 (6.0.922.41905), X64 RyuJIT AVX2


|                     Method |       Mean |   Error |  StdDev |   Gen0 | Allocated |
|--------------------------- |-----------:|--------:|--------:|-------:|----------:|
| MsgPack_SerializeLogRecord |   625.7 ns | 3.18 ns | 2.97 ns | 0.0038 |      24 B |
|     TLD_SerializeLogRecord |   331.1 ns | 1.57 ns | 1.47 ns |      - |         - |
|    MsgPack_ExportLogRecord | 1,017.1 ns | 6.02 ns | 5.64 ns | 0.0038 |      24 B |
|        TLD_ExportLogRecord |   695.0 ns | 1.80 ns | 1.69 ns |      - |         - |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark.Exporter
{
    [MemoryDiagnoser]
    public class TLDLogExporterBenchmarks
    {
        private readonly LogRecord logRecord;
        private readonly Batch<LogRecord> batch;
        private readonly GenevaLogExporter msgPackExporter;
        private readonly TLDLogExporter tldExporter;
        private readonly ILogger loggerForTLD;
        private readonly ILogger loggerForMsgPack;
        private readonly ILoggerFactory loggerFactoryForTLD;
        private readonly ILoggerFactory loggerFactoryForMsgPack;

        public TLDLogExporterBenchmarks()
        {
            this.msgPackExporter = new GenevaLogExporter(new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                },
            });

            this.tldExporter = new TLDLogExporter(new GenevaExporterOptions()
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                },
            });

            this.logRecord = GenerateTestLogRecord();
            this.batch = GenerateTestLogRecordBatch();

            this.loggerFactoryForTLD = LoggerFactory.Create(builder =>
                builder.AddOpenTelemetry(loggerOptions =>
                {
                    loggerOptions.AddTLDLogExporter(exporterOptions =>
                    {
                        exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
                        exporterOptions.PrepopulatedFields = new Dictionary<string, object>
                        {
                            ["cloud.role"] = "BusyWorker",
                            ["cloud.roleInstance"] = "CY1SCH030021417",
                            ["cloud.roleVer"] = "9.0.15289.2",
                        };
                    });
                }));

            this.loggerForTLD = this.loggerFactoryForTLD.CreateLogger<TLDLogExporterBenchmarks>();

            this.loggerFactoryForMsgPack = LoggerFactory.Create(builder =>
                builder.AddOpenTelemetry(loggerOptions =>
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
                }));

            this.loggerForMsgPack = this.loggerFactoryForMsgPack.CreateLogger<TLDLogExporterBenchmarks>();
        }

        [Benchmark]
        public void MsgPack_SerializeLogRecord()
        {
            this.msgPackExporter.SerializeLogRecord(this.logRecord);
        }

        [Benchmark]
        public void TLD_SerializeLogRecord()
        {
            this.tldExporter.SerializeLogRecord(this.logRecord);
        }

        [Benchmark]
        public void MsgPack_ExportLogRecord()
        {
            this.msgPackExporter.Export(this.batch);
        }

        [Benchmark]
        public void TLD_ExportLogRecord()
        {
            this.tldExporter.Export(this.batch);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.batch.Dispose();
            this.loggerFactoryForTLD.Dispose();
            this.loggerFactoryForMsgPack.Dispose();
            this.tldExporter.Dispose();
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
            logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
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
}
