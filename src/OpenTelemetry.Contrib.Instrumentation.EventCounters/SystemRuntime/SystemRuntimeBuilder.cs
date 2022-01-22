// <copyright file="SystemRuntimeBuilder.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.SystemRuntime
{
    internal class SystemRuntimeBuilder : ISystemRuntimeBuilder
    {
        private readonly EventSourceOption option;

        public SystemRuntimeBuilder(EventCountersOptions options)
        {
            this.option = options.AddEventSource(KnownEventSources.SystemRuntime);
        }

        public ISystemRuntimeBuilder With(params string[] counterNames)
        {
            this.option.AddEventCounters(counterNames);
            return this;
        }

        public ISystemRuntimeBuilder WithAllocationRate(string? metricName = null)
        {
            this.option.EventCounters.Add(new EventCounter
            {
                Name = "alloc-rate",
                Description = "The number of bytes allocated per update interval",
                Type = MetricType.Counter,
                MetricName = metricName,
            });

            return this;
        }

        public ISystemRuntimeBuilder WithPercentOfTimeinGCSinceLastGC(string? metricName = null)
        {
            this.option.EventCounters.Add(new EventCounter
            {
                Name = "time-in-gc",
                Description = "The percent of time in GC since the last GC",
                Type = MetricType.Counter,
                MetricName = metricName,
            });

            return this;
        }
    }
}
