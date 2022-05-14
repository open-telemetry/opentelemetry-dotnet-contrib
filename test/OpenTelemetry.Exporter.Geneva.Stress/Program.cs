// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Geneva.Stress
{
    internal class Program
    {
        private static volatile bool s_bContinue = true;
        private static long s_nEvents = 0;
        private static volatile string output = "Test results not available yet.";
        private static ActivitySource source = new ActivitySource("OpenTelemetry.Exporter.Geneva.Stress");

        private static int Main(string[] args)
        {
            // return Parser.Default.ParseArguments<WindowsOptions, LinuxOptions, ServerOptions, ExporterCreationOptions>(args)
            //    .MapResult(
            //        (WindowsOptions options) => EntryPoint(InitTraces, RunTraces),
            //        (LinuxOptions options) => RunLinux(options),
            //        (ServerOptions options) => RunServer(options),
            //        (ExporterCreationOptions options) => RunExporterCreation(),
            //        errs => 1);

            return EntryPoint();
        }

        [Verb("Windows", HelpText = "Run stress test on Windows.")]
        private class WindowsOptions
        {
        }

        [Verb("Linux", HelpText = "Run stress test on Linux.")]
        private class LinuxOptions
        {
            [Option('p', "path", Default = "/var/run/default_fluent.socket", HelpText = "Specify a path for Unix domain socket.")]
            public string Path { get; set; }
        }

        [Verb("server", HelpText = "Start a dummy server on Linux.")]
        private class ServerOptions
        {
            [Option('p', "path", HelpText = "Specify a path for Unix domain socket.", Required = true)]
            public string Path { get; set; }
        }

        [Verb("ExporterCreation", HelpText = "Validate exporter dispose behavior")]
        private class ExporterCreationOptions
        {
        }

        private static int RunExporterCreation()
        {
            var options = new GenevaExporterOptions()
            {
                ConnectionString = "EtwSession=OpenTelemetry",
                PrepopulatedFields = new Dictionary<string, object>
                {
                    ["ver"] = "4.0",
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                },
            };

            for (var i = 0; i < 300000; ++i)
            {
                using var dataTransport = new EtwDataTransport("OpenTelemetry");
            }

            return 0;
        }

        // private static int RunLinux(LinuxOptions options)
        // {
        //     return EntryPoint(() => InitTracesOnLinux(options.Path), RunTraces);
        // }

        private static int RunServer(ServerOptions options)
        {
            var server = new DummyServer(options.Path);
            server.Start();
            return 0;
        }

        private static int EntryPoint()
        {
            int maxCapacity = 1000;
            Random random = new Random(97);
            List<ILogger> loggers = new(maxCapacity);

            var cntLoopsTotal = 0UL;
            var dLoopsPerSecond = 0D;
            var dCpuCyclesPerLoop = 0D;

            var loggerFactory = LoggerFactory.Create(builder =>
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
                            ["Company.StoreA"] = "Store",
                            ["*"] = "*",
                        };
                    });
                });
            });

            for (int i = 0; i < maxCapacity; ++i)
            {
                loggers.Add(loggerFactory.CreateLogger("Company-%-Customer*Region$##" + (i + maxCapacity).ToString()));
            }

            var statistics = new long[Environment.ProcessorCount];
            var watchForTotal = Stopwatch.StartNew();

            Parallel.Invoke(
            () =>
            {
                Console.WriteLine("Running, press <Esc> to stop...");

                var bOutput = false;
                var watch = new Stopwatch();
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        switch (key)
                        {
                            case ConsoleKey.Enter:
                                Console.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToString("O"), output));
                                break;
                            case ConsoleKey.Escape:
                                s_bContinue = false;
                                return;
                            case ConsoleKey.Spacebar:
                                bOutput = !bOutput;
                                break;
                        }

                        continue;
                    }

                    if (bOutput)
                    {
                        Console.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToString("O"), output));
                    }

                    var cntLoopsOld = (ulong)statistics.Sum();
                    var cntCpuCyclesOld = GetCpuCycles();

                    watch.Restart();
                    Thread.Sleep(200);
                    watch.Stop();

                    cntLoopsTotal = (ulong)statistics.Sum();
                    var cntCpuCyclesNew = GetCpuCycles();

                    var nLoops = cntLoopsTotal - cntLoopsOld;
                    var nCpuCycles = cntCpuCyclesNew - cntCpuCyclesOld;

                    dLoopsPerSecond = (double)nLoops / ((double)watch.ElapsedMilliseconds / 1000.0);
                    dCpuCyclesPerLoop = nLoops == 0 ? 0 : nCpuCycles / nLoops;

                    var nEvents = statistics.Sum();
                    var nEventPerSecond = (int)((nEvents - s_nEvents) / (watch.ElapsedMilliseconds / 1000.0));

                    output = $"Loops: {cntLoopsTotal:n0}, Loops/Second: {dLoopsPerSecond:n0}, CPU Cycles/Loop: {dCpuCyclesPerLoop:n0}";
                    Console.Title = output;
                }
            },
            () =>
            {
                Parallel.For(0, statistics.Length, (i) =>
                {
                    statistics[i] = 0;
                    while (s_bContinue)
                    {
                        loggers[random.Next(0, maxCapacity)].LogInformation("Hello from {storeName} {number}.", "Kyoto", 2);
                        statistics[i]++;
                    }
                });
            });

            watchForTotal.Stop();
            cntLoopsTotal = (ulong)statistics.Sum();
            var totalLoopsPerSecond = (double)cntLoopsTotal / ((double)watchForTotal.ElapsedMilliseconds / 1000.0);
            var cntCpuCyclesTotal = GetCpuCycles();
            var cpuCyclesPerLoopTotal = cntLoopsTotal == 0 ? 0 : cntCpuCyclesTotal / cntLoopsTotal;
            Console.WriteLine("Stopping the stress test...");
            Console.WriteLine($"* Total Loops: {cntLoopsTotal:n0}");
            Console.WriteLine($"* Average Loops/Second: {totalLoopsPerSecond:n0}");
            Console.WriteLine($"* Average CPU Cycles/Loop: {cpuCyclesPerLoopTotal:n0}");

            return 0;
        }

        private static void InitTraces()
        {
            Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource("OpenTelemetry.Exporter.Geneva.Stress")
                .AddGenevaTraceExporter(options =>
                {
                    options.ConnectionString = "EtwSession=OpenTelemetry";
                    options.PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    };
                })
                .Build();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RunTraces()
        {
            using (var activity = source.StartActivity("Stress"))
            {
                activity?.SetTag("http.method", "GET");
                activity?.SetTag("http.url", "https://www.wikipedia.org/wiki/Rabbit");
                activity?.SetTag("http.status_code", 200);
            }
        }

        private static void InitTracesOnLinux(string path)
        {
            Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource("OpenTelemetry.Exporter.Geneva.Stress")
                .AddGenevaTraceExporter(options =>
                {
                    options.ConnectionString = "Endpoint=unix:" + path;
                    options.PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    };
                })
                .Build();
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryProcessCycleTime(IntPtr hProcess, out ulong cycles);

        private static ulong GetCpuCycles()
        {
            if (!QueryProcessCycleTime((IntPtr)(-1), out var cycles))
            {
                return 0;
            }

            return cycles;
        }
    }
}
