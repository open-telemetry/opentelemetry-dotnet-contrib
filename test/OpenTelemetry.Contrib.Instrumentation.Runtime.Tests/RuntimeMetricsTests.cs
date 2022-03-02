// <copyright file="RuntimeMetricsTests.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Tests
{
    public class RuntimeMetricsTests
    {
        private MeterProvider meterProvider = null;

        [Fact]
        public async Task RequestMetricIsCaptured()
        {
            var metricItems = new List<Metric>();
            var metricExporter = new InMemoryExporter<Metric>(metricItems);

            var metricReader = new PeriodicExportingMetricReader(metricExporter, 500);

            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddRuntimeMetrics(options =>
                 {
                     options.GcEnabled = true;
#if NETCOREAPP3_1_OR_GREATER
                     options.ThreadingEnabled = true;
#endif
                     options.MemoryEnabled = true;
#if NET6_0_OR_GREATER

                     options.JitEnabled = true;
#endif
                     options.AssembliesEnabled = true;
                 })
                 .AddReader(metricReader)
                .Build();

            await Task.Delay(TimeSpan.FromSeconds(2));
            this.meterProvider.Dispose();

            Assert.True(metricItems.Count > 1);
        }
    }
}
