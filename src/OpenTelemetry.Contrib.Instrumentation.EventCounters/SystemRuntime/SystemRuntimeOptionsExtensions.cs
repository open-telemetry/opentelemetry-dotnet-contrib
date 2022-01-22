// <copyright file="SystemRuntimeOptionsExtensions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Contrib.Instrumentation.EventCounters;
using OpenTelemetry.Contrib.Instrumentation.EventCounters.SystemRuntime;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods for the event counters options.
    /// </summary>
    public static class SystemRuntimeOptionsExtensions
    {
        /// <summary>
        /// Adds the System.Runtime event source. Adds all counters from <a href="https://docs.microsoft.com/en-us/dotnet/core/diagnostics/available-counters#systemruntime-counters">well-known event counters</a>.
        /// </summary>
        /// <param name="options">The options to add the event source to.</param>
        /// <returns>The builder instance to define event counters.</returns>
        public static ISystemRuntimeBuilder AddRuntime(this EventCountersOptions options)
        {
            return new SystemRuntimeBuilder(options);
        }
    }
}
