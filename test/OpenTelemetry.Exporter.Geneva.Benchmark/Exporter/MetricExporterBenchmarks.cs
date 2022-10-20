// <copyright file="MetricExporterBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Instrumentation.Process;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva.Benchmark;

[MemoryDiagnoser]
public class MetricExporterBenchmarks
{
    private MeterProvider meterProvider;
    private BaseExportingMetricReader inMemoryReader;
    private List<Metric> exportedItems = new List<Metric>();

    private MeterProvider meterProvider2;
    private BaseExportingMetricReader inMemoryReader2;
    private List<Metric> exportedItems2 = new List<Metric>();

    private MeterProvider meterProvider3;
    private BaseExportingMetricReader inMemoryReader3;
    private List<Metric> exportedItems3 = new List<Metric>();

    [GlobalSetup]
    public void Setup()
    {
        this.inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(this.exportedItems));
        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentationRefreshOnce()
            .AddReader(this.inMemoryReader)
            .Build();

        this.inMemoryReader2 = new BaseExportingMetricReader(new InMemoryExporter<Metric>(this.exportedItems2));
        this.meterProvider2 = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentationRefreshEachTime()
            .AddReader(this.inMemoryReader2)
            .Build();

        this.inMemoryReader3 = new BaseExportingMetricReader(new InMemoryExporter<Metric>(this.exportedItems3));
        this.meterProvider3 = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentationNewProcessInstance()
            .AddReader(this.inMemoryReader3)
            .Build();
    }

    [Benchmark]
    public void RefreshOnce()
    {
        this.inMemoryReader.Collect();
    }

    [Benchmark]
    public void RefreshEachCallbackWithLock()
    {
        this.inMemoryReader2.Collect();
    }

    [Benchmark]
    public void CreateNewProcessInstanceOnEachCallback()
    {
        this.inMemoryReader3.Collect();
    }

    public sealed class ProcessMetricsRefreshOnce
    {
        internal static readonly AssemblyName AssemblyName = typeof(ProcessMetricsRefreshOnce).Assembly.GetName();
        internal readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

        private readonly System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

        public ProcessMetricsRefreshOnce(ProcessInstrumentationOptions options)
        {
            this.MeterInstance.CreateObservableGauge(
                "gaugeA",
                () =>
                {
                    this.currentProcess.Refresh();
                    return 1D;
                },
                unit: "1",
                description: "desA");

            this.MeterInstance.CreateObservableGauge(
                "gaugeB",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desB");

            this.MeterInstance.CreateObservableCounter(
                "gaugeC",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desC");

            this.MeterInstance.CreateObservableGauge(
                "gaugeD",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desD");

            this.MeterInstance.CreateObservableGauge(
                "gaugeE",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desE");

            this.MeterInstance.CreateObservableGauge(
                "gaugeF",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desF");

            this.MeterInstance.CreateObservableGauge(
                "gaugeG",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desG");

            this.MeterInstance.CreateObservableCounter(
                "gaugeH",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desH");

            this.MeterInstance.CreateObservableGauge(
                "gaugeI",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desI");

            this.MeterInstance.CreateObservableGauge(
                "gaugeJ",
                () =>
                {
                    return 1D;
                },
                unit: "1",
                description: "desJ");
        }
    }

    public sealed class ProcessMetricsRefreshEachTime
    {
        internal static readonly AssemblyName AssemblyName = typeof(ProcessMetricsRefreshEachTime).Assembly.GetName();
        internal readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

        private readonly System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();

        public ProcessMetricsRefreshEachTime(ProcessInstrumentationOptions options)
        {
            this.MeterInstance.CreateObservableGauge(
                "gaugeA",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desA");

            this.MeterInstance.CreateObservableGauge(
                "gaugeB",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desB");

            this.MeterInstance.CreateObservableCounter(
                "gaugeC",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desC");

            this.MeterInstance.CreateObservableGauge(
                "gaugeD",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desD");

            this.MeterInstance.CreateObservableGauge(
                "gaugeE",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desE");

            this.MeterInstance.CreateObservableGauge(
                "gaugeF",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desF");

            this.MeterInstance.CreateObservableGauge(
                "gaugeG",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desG");

            this.MeterInstance.CreateObservableCounter(
                "gaugeH",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desH");

            this.MeterInstance.CreateObservableGauge(
                "gaugeI",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desI");

            this.MeterInstance.CreateObservableGauge(
                "gaugeJ",
                () =>
                {
                    lock (this)
                    {
                        this.currentProcess.Refresh();
                        return 1D;
                    }
                },
                unit: "1",
                description: "desJ");
        }
    }

    public sealed class ProcessMetricsNewProcessInstanceOnEachCallback
    {
        internal static readonly AssemblyName AssemblyName = typeof(ProcessMetricsNewProcessInstanceOnEachCallback).Assembly.GetName();
        internal readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

        public ProcessMetricsNewProcessInstanceOnEachCallback(ProcessInstrumentationOptions options)
        {
            this.MeterInstance.CreateObservableGauge(
                "gaugeA",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desA");

            this.MeterInstance.CreateObservableGauge(
                "gaugeB",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desB");

            this.MeterInstance.CreateObservableCounter(
                "gaugeC",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desC");

            this.MeterInstance.CreateObservableGauge(
                "gaugeD",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desD");

            this.MeterInstance.CreateObservableGauge(
                "gaugeE",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desE");

            this.MeterInstance.CreateObservableGauge(
                "gaugeF",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desF");

            this.MeterInstance.CreateObservableGauge(
                "gaugeG",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desG");

            this.MeterInstance.CreateObservableCounter(
                "gaugeH",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desH");

            this.MeterInstance.CreateObservableGauge(
                "gaugeI",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desI");

            this.MeterInstance.CreateObservableGauge(
                "gaugeJ",
                () =>
                {
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    return 1D;
                },
                unit: "1",
                description: "desJ");
        }
    }
}
