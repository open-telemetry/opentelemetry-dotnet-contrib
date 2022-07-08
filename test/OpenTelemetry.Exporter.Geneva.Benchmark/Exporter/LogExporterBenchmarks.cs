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

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1766 (21H2)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.301
  [Host]     : .NET 6.0.6 (6.0.622.26707), X64 RyuJIT
  DefaultJob : .NET 6.0.6 (6.0.622.26707), X64 RyuJIT


|                    Method | IncludeFormattedMessage |       Mean |    Error |   StdDev |  Gen 0 | Allocated |
|-------------------------- |------------------------ |-----------:|---------:|---------:|-------:|----------:|
| LoggerWithMessageTemplate |                   False | 1,107.3 ns | 19.34 ns | 17.15 ns | 0.0858 |     360 B |
| LoggerWithDirectLoggerAPI |                   False | 1,027.4 ns |  8.92 ns |  7.91 ns | 0.1183 |     496 B |
| LoggerWithSourceGenerator |                   False | 1,081.5 ns |  4.53 ns |  4.24 ns | 0.0763 |     320 B |
|        SerializeLogRecord |                   False |   825.1 ns |  6.88 ns |  5.74 ns | 0.0305 |     128 B |
| LoggerWithMessageTemplate |                    True | 1,123.3 ns |  2.24 ns |  1.87 ns | 0.0858 |     360 B |
| LoggerWithDirectLoggerAPI |                    True | 1,005.8 ns |  2.26 ns |  2.00 ns | 0.1183 |     496 B |
| LoggerWithSourceGenerator |                    True | 1,083.9 ns |  8.73 ns |  6.82 ns | 0.0763 |     320 B |
|        SerializeLogRecord |                    True |   827.1 ns |  6.45 ns |  6.03 ns | 0.0305 |     128 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark
{
    [MemoryDiagnoser]
    public class LogExporterBenchmarks
    {
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly GenevaLogExporter exporter;
        private readonly LogRecord logRecord;

        [Params(true, false)]
        public bool IncludeFormattedMessage { get; set; }

        public LogExporterBenchmarks()
        {
            // for total cost of logging + msgpack serialization
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

            // For msgpack serialization alone
            this.logRecord = GenerateTestLogRecord();
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

        internal static LogRecord GenerateTestLogRecord()
        {
            var items = new List<LogRecord>(1);
            var factory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(loggerOptions =>
                {
                    loggerOptions.AddInMemoryExporter(items);
                }));

            var logger = factory.CreateLogger("SerializationTest");
            logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
            return items[0];
        }
    }
}
