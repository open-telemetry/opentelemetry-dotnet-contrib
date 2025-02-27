// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Diagnostics;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.LinuxTracepoints.Provider;
using OpenTelemetry.Exporter.Geneva.EventHeader;
using OpenTelemetry.Exporter.Geneva.MsgPack;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet v0.13.12, Ubuntu 24.04.1 LTS (Noble Numbat) WSL
AMD EPYC 7763, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.103
  [Host]     : .NET 8.0.12 (8.0.1224.60305), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.12 (8.0.1224.60305), X64 RyuJIT AVX2


| Method                        | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------------ |------------:|----------:|----------:|-------:|----------:|
| MsgPack_SerializeLogRecord    |    435.9 ns |   2.11 ns |   1.98 ns |      - |         - |
| UserEvents_SerializeLogRecord |    636.3 ns |   5.17 ns |   4.58 ns | 0.0057 |     104 B |
| MsgPack_ExportLogRecord       | 54,254.0 ns | 655.46 ns | 613.12 ns | 0.0610 |    1552 B |
| UserEvents_ExportLogRecord    |  3,524.6 ns |  29.26 ns |  25.94 ns | 0.0038 |     104 B |

$ uname -r
6.6.36.3-microsoft-standard-WSL2
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

    public UserEventsLogExporterBenchmarks()
    {
        var listener = new PerfTracepointListener(
            "MicrosoftOpenTelemetryLogs_L4K1",
            MetricUnixUserEventsDataTransport.MetricsTracepointNameArgs);

        listener.Enable();

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

    private sealed class ConsoleCommand : IDisposable
    {
        private readonly Process process;
        private readonly List<string> output = [];
        private readonly List<string> errors = [];

        private ConsoleCommand(
            string command,
            string arguments,
            Action<string>? onOutputReceived,
            Action<string>? onErrorReceived)
        {
            Console.WriteLine($"{command} {arguments}");

            var process = new Process
            {
                StartInfo = new()
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                },
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    this.output.Add(args.Data);
                    Console.WriteLine($"{command} {arguments} [OUT] {args.Data}");

                    onOutputReceived?.Invoke(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    this.errors.Add(args.Data);
                    Console.WriteLine($"[ERR] {args.Data}");

                    onErrorReceived?.Invoke(args.Data);
                }
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            this.process = process;
        }

        public IEnumerable<string> Output => this.output;

        public IEnumerable<string> Errors => this.errors;

        public static IEnumerable<string> Run(
            string command,
            string arguments)
        {
            Run(command, arguments, out _, out var errors);

            return errors;
        }

        public static void Run(
            string command,
            string arguments,
            out IEnumerable<string> output,
            out IEnumerable<string> errors)
        {
            var consoleCommand = new ConsoleCommand(command, arguments, onOutputReceived: null, onErrorReceived: null);
            consoleCommand.Dispose();

            output = consoleCommand.Output;
            errors = consoleCommand.Errors;
        }

        public static ConsoleCommand Start(
            string command,
            string arguments,
            Action<string>? onOutputReceived = null,
            Action<string>? onErrorReceived = null)
            => new(command, arguments, onOutputReceived, onErrorReceived);

        public void Kill()
        {
            this.process.Kill();
        }

        public void Dispose()
        {
            this.process.WaitForExit();

            this.process.CancelOutputRead();
            this.process.CancelErrorRead();

            this.process.Dispose();
        }
    }

    // Warning: Do NOT use this class/design to listen/read user_events in prod.
    // It is a hack to workaround lack of decent bits for listening. Hopefully
    // this can be removed if/when
    // https://github.com/microsoft/LinuxTracepoints-Net/ has listening bits or
    // dotnet/runtime supports user_events (both reading & writing) directly.
    private sealed class PerfTracepointListener : IDisposable
    {
        private readonly string name;
        private readonly PerfTracepoint tracepoint;
        private readonly ConsoleCommand catCommand;
        private readonly Regex eventRegex = new("(\\w+?)=([\\w\\(\\) .,-]*)( |$)", RegexOptions.Compiled);

        public PerfTracepointListener(string name, string nameArgs)
        {
            this.name = name;

            this.tracepoint = new PerfTracepoint(nameArgs);

            // EACCES (13): Permission denied
            if (this.tracepoint.RegisterResult == 13)
            {
                throw new UnauthorizedAccessException($"Tracepoint could not be registered: '{this.tracepoint.RegisterResult}'. Permission denied.");
            }

            if (this.tracepoint.RegisterResult != 0)
            {
                throw new NotSupportedException($"Tracepoint could not be registered: '{this.tracepoint.RegisterResult}'");
            }

            this.catCommand = ConsoleCommand.Start("cat", "/sys/kernel/debug/tracing/trace_pipe", onOutputReceived: this.OnCatOutputReceived);
            if (this.catCommand.Errors.Any())
            {
                throw new InvalidOperationException($"Could not read '{name}' tracepoints");
            }
        }

        public List<Dictionary<string, string>> Events { get; } = [];

        public bool IsEnabled()
        {
            ConsoleCommand.Run(
                "cat",
                $"/sys/kernel/tracing/events/user_events/{this.name}/enable",
                out var output,
                out var errors);

            return errors.Any() || output.Count() != 1
                ? throw new InvalidOperationException($"Could not determine if '{this.name}' tracepoint is enabled")
                : output.First() != "0";
        }

        public void Enable()
        {
            var errors = ConsoleCommand.Run("sh", @$"-c ""echo '1' > /sys/kernel/tracing/events/user_events/{this.name}/enable""");

            if (errors.Any())
            {
                throw new InvalidOperationException($"Could not enable '{this.name}' tracepoint");
            }
        }

        public void Disable()
        {
            var errors = ConsoleCommand.Run("sh", @$"-c ""echo '0' > /sys/kernel/tracing/events/user_events/{this.name}/enable""");

            if (errors.Any())
            {
                throw new InvalidOperationException($"Could not disable '{this.name}' tracepoint");
            }
        }

        public void Dispose()
        {
            try
            {
                if (this.catCommand != null)
                {
                    if (this.catCommand.Errors.Any())
                    {
                        throw new InvalidOperationException($"Could not read '{this.name}' tracepoints");
                    }

                    this.catCommand.Kill();
                    this.catCommand.Dispose();
                }
            }
            finally
            {
                this.tracepoint.Dispose();
            }
        }

        private void OnCatOutputReceived(string output)
        {
            var name = $": {this.name}:";

            var startingPosition = output.IndexOf(name, StringComparison.Ordinal);
            if (startingPosition < 0)
            {
                return;
            }

            startingPosition += name.Length;

            var matches = this.eventRegex.Matches(output, startingPosition);

            if (matches.Count > 0)
            {
                Dictionary<string, string> eventData = new(matches.Count);

                foreach (Match match in matches)
                {
                    eventData[match.Groups[1].Value] = match.Groups[2].Value;
                }

                this.Events.Add(eventData);
            }
        }
    }
}

#endif
