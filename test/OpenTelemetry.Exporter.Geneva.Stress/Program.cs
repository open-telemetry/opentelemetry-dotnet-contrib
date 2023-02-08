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
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Geneva.Stress;

internal class Program
{
    private static volatile bool s_bContinue = true;
    private static long s_nEvents;

    private static ActivitySource source = new ActivitySource("OpenTelemetry.Exporter.Geneva.Stress");

    private static int Main(string[] args)
    {
        return Parser.Default.ParseArguments<WindowsOptions, LinuxOptions, ServerOptions, ExporterCreationOptions>(args)
            .MapResult(
                (WindowsOptions options) => EntryPoint(InitTraces, RunTraces),
                (LinuxOptions options) => RunLinux(options),
                (ServerOptions options) => RunServer(options),
                (ExporterCreationOptions options) => RunExporterCreation(),
                errs => 1);

        // return EntryPoint(InitMetrics, RunMetrics);
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

    private static int RunLinux(LinuxOptions options)
    {
        return EntryPoint(() => InitTracesOnLinux(options.Path), RunTraces);
    }

    private static int RunServer(ServerOptions options)
    {
        var server = new DummyServer(options.Path);
        server.Start();
        return 0;
    }

    private static int EntryPoint(Action init, Action run)
    {
        init();

        var statistics = new long[Environment.ProcessorCount];
        Parallel.Invoke(
            () =>
            {
                Console.WriteLine("Running, press <Esc> to stop...");
                var watch = new Stopwatch();
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        switch (key)
                        {
                            case ConsoleKey.Escape:
                                s_bContinue = false;
                                return;
                        }

                        continue;
                    }

                    s_nEvents = statistics.Sum();
                    watch.Restart();
                    Thread.Sleep(200);
                    watch.Stop();
                    var nEvents = statistics.Sum();
                    var nEventPerSecond = (int)((nEvents - s_nEvents) / (watch.ElapsedMilliseconds / 1000.0));
                    Console.Title = string.Format(CultureInfo.InvariantCulture, "Loops: {0:n0}, Loops/Second: {1:n0}", nEvents, nEventPerSecond);
                }
            },
            () =>
            {
                Parallel.For(0, statistics.Length, (i) =>
                {
                    statistics[i] = 0;
                    while (s_bContinue)
                    {
                        run();
                        statistics[i]++;
                    }
                });
            });
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
}
