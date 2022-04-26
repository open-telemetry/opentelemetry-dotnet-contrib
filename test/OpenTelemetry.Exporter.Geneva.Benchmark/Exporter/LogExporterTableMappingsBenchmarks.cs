using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

/*
BenchmarkDotNet = v0.13.1, OS = Windows 10.0.19044.1645(21H2)
Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.202
  [Host]     : .NET Core 3.1.24 (CoreCLR 4.700.22.16002, CoreFX 4.700.22.17909), X64 RyuJIT
  DefaultJob : .NET Core 3.1.24 (CoreCLR 4.700.22.16002, CoreFX 4.700.22.17909), X64 RyuJIT


|                                          Method |     Mean |     Error |    StdDev |   Median |  Gen 0 | Allocated |
|------------------------------------------------ |---------:|----------:|----------:|---------:|-------:|----------:|
| CategoryTableNameMappingsDefinedInConfiguration | 1.873 us | 0.0442 us | 0.1218 us | 1.839 us | 0.0610 |     256 B |
|    PassThruTableNameMappingsWhenTheRuleIsEnbled | 1.824 us | 0.0358 us | 0.0317 us | 1.817 us | 0.0877 |     368 B |
*/

namespace OpenTelemetry.Exporter.Geneva.Benchmark
{
    [MemoryDiagnoser]
    public class LogExporterTableMappingsBenchmarks
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger storeLogger;
        private readonly ILogger customerLogger;

        public LogExporterTableMappingsBenchmarks()
        {
            this.loggerFactory = LoggerFactory.Create(builder =>
            {
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

                        exporterOptions.TableNameMappings = new Dictionary<string, string>
                        {
                            ["Company.Store"] = "Store",
                            ["*"] = "*",
                        };
                    });
                });
            });

            this.storeLogger = this.loggerFactory.CreateLogger("Company.Store");
            this.customerLogger = this.loggerFactory.CreateLogger("Company.Customer");
        }

        [Benchmark]
        public void CategoryTableNameMappingsDefinedInConfiguration()
        {
            this.storeLogger.LogInformation("Hello from {storeName} {number}.", "Tokyo", 6);
        }

        [Benchmark]
        public void PassThruTableNameMappingsWhenTheRuleIsEnbled()
        {
            this.customerLogger.LogInformation("Hello from {customerName} {amount}.", "Gakki", 1.75);
        }
    }
}
