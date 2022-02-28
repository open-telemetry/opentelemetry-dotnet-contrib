// <copyright file="PerformanceInstrumentation.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    internal class PerformanceInstrumentation : IRuntimeInstrumentation
    {
        private readonly Meter meter;
        private readonly ObservableGauge<double> workingSetCounter;

        public PerformanceInstrumentation(RuntimeMetricsOptions options, Meter meter)
        {
            this.meter = meter;

            this.workingSetCounter = meter.CreateObservableGauge($"{options.MetricPrefix}working_set", () => (double)(Environment.WorkingSet / 1_000_000), "MB", "Working Set");
        }
    }
}
