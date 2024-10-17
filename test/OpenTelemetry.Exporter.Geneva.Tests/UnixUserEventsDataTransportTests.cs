// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET6_0_OR_GREATER

#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.LinuxTracepoints.Provider;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Exporter.Geneva.Tests;

[Trait("CategoryName", "Geneva:user_events:metrics")]
public class UnixUserEventsDataTransportTests
{
    /*
     * Instructions for running these tests:
     *
     *  1) You need a version of Linux with user_events available in the kernel.
     *     This can be done on WSL2 using the 6.6+ kernel.
     *
     *  2) You have to run the tests with elevation. You don't need elevation to
     *     write/emit user_events but you need elevation to read them (which
     *     these tests do).
     *
     *  Command:
     *    sudo dotnet test --configuration Debug --framework net8.0 --filter CategoryName=Geneva:user_events:metrics
     *
     * How these tests work:
     *
     *  1) The tests validate user_events are enabled and make sure the otlp_metrics tracepoint is registered.
     *
     *  2) A process is spawned to run cat /sys/kernel/debug/tracing/trace_pipe. This is what is listening for events.
     *
     *  3) Depending on the test, a process is spawned to run sh -c "echo '1' > /sys/kernel/tracing/events/user_events/{this.name}/enable" to enable events.
     *
     *  4) The thread running the tests writes to user_events using the GenevaExporter code. Then it waits for a bit. Then it checks to see what events (if any) were emitted.
     *
     *  5) Depending on the test, a process is spawned to run sh -c "echo '0' > /sys/kernel/tracing/events/user_events/{this.name}/enable" to disable events.
     */

    private static readonly byte[] testRequest = [0x0a, 0x0f, 0x12, 0x0d, 0x0a, 0x0b, 0x0a, 0x09, 0x54, 0x65, 0x73, 0x74, 0x4d, 0x65, 0x74, 0x65, 0x72];
    private readonly ITestOutputHelper testOutputHelper;

    public UnixUserEventsDataTransportTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux, requireElevatedProcess: true)]
    public void UserEvents_Enabled_Succes_Linux()
    {
        EnsureUserEventsEnabled();

        var listener = new PerfTracepointListener(
            MetricUnixUserEventsDataTransport.MetricsTracepointName,
            MetricUnixUserEventsDataTransport.MetricsTracepointNameArgs);

        if (listener.IsEnabled())
        {
            throw new NotSupportedException($"{MetricUnixUserEventsDataTransport.MetricsTracepointName} is already enabled");
        }

        try
        {
            listener.Enable();

            MetricUnixUserEventsDataTransport.Instance.SendOtlpProtobufEvent(
                testRequest,
                testRequest.Length);

            Thread.Sleep(5000);

            foreach (var e in listener.Events)
            {
                this.testOutputHelper.WriteLine(string.Join(", ", e.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            Assert.Single(listener.Events);

            var @event = listener.Events[0];

            Assert.EndsWith($" ({MetricUnixUserEventsDataTransport.MetricsProtocol})", @event["protocol"]);
            Assert.Equal(MetricUnixUserEventsDataTransport.MetricsVersion, @event["version"]);

            var eventBufferStringData = @event["buffer"].AsSpan();

            byte[] eventBuffer = new byte[(eventBufferStringData.Length + 1) / 3];

            var index = 0;
            var position = 0;
            while (position < eventBufferStringData.Length)
            {
                eventBuffer[index++] = byte.Parse(eventBufferStringData.Slice(position, 2), NumberStyles.HexNumber);
                position += 3;
            }

            Assert.Equal(testRequest, eventBuffer);
        }
        finally
        {
            try
            {
                listener.Disable();
            }
            catch
            {
            }

            listener.Dispose();
        }
    }

    [SkipUnlessPlatformMatchesFact(TestPlatform.Linux, requireElevatedProcess: true)]
    public void UserEvents_Disabled_Succes_Linux()
    {
        EnsureUserEventsEnabled();

        var listener = new PerfTracepointListener(
            MetricUnixUserEventsDataTransport.MetricsTracepointName,
            MetricUnixUserEventsDataTransport.MetricsTracepointNameArgs);

        if (listener.IsEnabled())
        {
            throw new NotSupportedException($"{MetricUnixUserEventsDataTransport.MetricsTracepointName} is already enabled");
        }

        try
        {
            MetricUnixUserEventsDataTransport.Instance.SendOtlpProtobufEvent(
                testRequest,
                testRequest.Length);

            Thread.Sleep(5000);

            Assert.Empty(listener.Events);
        }
        finally
        {
            listener.Dispose();
        }
    }

    private static void EnsureUserEventsEnabled()
    {
        var errors = ConsoleCommand.Run("cat", "/sys/kernel/tracing/user_events_status");

        if (errors.Any())
        {
            throw new NotSupportedException("Kernel does not support user_events. Verify your distribution/kernel supports user_events: https://docs.kernel.org/trace/user_events.html.");
        }
    }

    private sealed class ConsoleCommand : IDisposable
    {
        private readonly Process process;
        private readonly List<string> output = new();
        private readonly List<string> errors = new();

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
                    Console.WriteLine($"[OUT] {args.Data}");

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

        public List<Dictionary<string, string>> Events { get; } = new();

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

            int startingPosition = output.IndexOf(name, StringComparison.Ordinal);
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
