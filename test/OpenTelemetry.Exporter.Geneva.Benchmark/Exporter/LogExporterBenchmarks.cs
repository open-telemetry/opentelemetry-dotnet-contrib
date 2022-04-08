using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1415 (21H2)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK=6.0.101
  [Host]     : .NET 5.0.12 (5.0.1221.52207), X64 RyuJIT
  DefaultJob : .NET 5.0.12 (5.0.1221.52207), X64 RyuJIT


```
|                   Method |        Mean |    Error |   StdDev |  Gen 0 | Allocated |
|------------------------- |------------:|---------:|---------:|-------:|----------:|
|               NoListener |    58.41 ns | 0.360 ns | 0.337 ns | 0.0076 |      64 B |
|             OneProcessor |   189.79 ns | 0.391 ns | 0.347 ns | 0.0277 |     232 B |
|            TwoProcessors |   195.50 ns | 0.438 ns | 0.388 ns | 0.0277 |     232 B |
|          ThreeProcessors |   198.98 ns | 0.500 ns | 0.468 ns | 0.0277 |     232 B |
| LoggerWithGenevaExporter | 1,101.36 ns | 4.134 ns | 3.452 ns | 0.0305 |     256 B |
|       SerializeLogRecord |   743.53 ns | 2.233 ns | 2.088 ns | 0.0029 |      24 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark
{
    [MemoryDiagnoser]
    public class LogExporterBenchmarks
    {
        private readonly LogRecord logRecord;
        private readonly GenevaLogExporter exporter;
        private readonly ILogger loggerWithNoListener;
        private readonly ILogger loggerWithGenevaExporter;
        private readonly ILogger loggerWithOneProcessor;
        private readonly ILogger loggerWithTwoProcessors;
        private readonly ILogger loggerWithThreeProcessors;

        public LogExporterBenchmarks()
        {
            logRecord = GenerateTestLogRecord();

            exporter = new GenevaLogExporter(new GenevaExporterOptions
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                },
            });

            loggerWithNoListener = CreateLogger();

            loggerWithOneProcessor = CreateLogger(options => options
                .AddProcessor(new DummyLogProcessor()));

            loggerWithTwoProcessors = CreateLogger(options => options
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor()));

            loggerWithThreeProcessors = CreateLogger(options => options
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor()));

            loggerWithGenevaExporter = CreateLogger(options =>
            {
                options.AddGenevaLogExporter(genevaOptions =>
                {
                    genevaOptions.ConnectionString = "EtwSession=OpenTelemetry";
                    genevaOptions.PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    };
                });
            });
        }

        [Benchmark]
        public void NoListener()
        {
            loggerWithNoListener.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
        }

        [Benchmark]
        public void OneProcessor()
        {
            loggerWithOneProcessor.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
        }

        [Benchmark]
        public void TwoProcessors()
        {
            loggerWithTwoProcessors.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
        }

        [Benchmark]
        public void ThreeProcessors()
        {
            loggerWithThreeProcessors.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
        }

        [Benchmark]
        public void LoggerWithGenevaExporter()
        {
            loggerWithGenevaExporter.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
        }

        [Benchmark]
        public void SerializeLogRecord()
        {
            exporter.SerializeLogRecord(logRecord);
        }

        internal class DummyLogProcessor : BaseProcessor<LogRecord>
        {
        }

        internal class DummyLogExporter : BaseExporter<LogRecord>
        {
            public LogRecord LastRecord { get; set; }

            public override ExportResult Export(in Batch<LogRecord> batch)
            {
                foreach (var record in batch)
                {
                    LastRecord = record;
                }

                return ExportResult.Success;
            }
        }

        internal ILogger CreateLogger(Action<OpenTelemetryLoggerOptions> configure = null)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                if (configure != null)
                {
                    builder.AddOpenTelemetry(configure);
                }
            });

            return loggerFactory.CreateLogger<LogExporterBenchmarks>();
        }

        internal LogRecord GenerateTestLogRecord()
        {
            var dummyLogExporter = new DummyLogExporter();
            var dummyLogger = CreateLogger(options => options
                .AddProcessor(new SimpleLogRecordExportProcessor(dummyLogExporter)));
            dummyLogger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
            return dummyLogExporter.LastRecord;
        }
    }
}
