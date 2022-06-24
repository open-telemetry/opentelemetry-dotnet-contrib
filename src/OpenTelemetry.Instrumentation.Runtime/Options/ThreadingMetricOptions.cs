// <copyright file="GcMetricOptions.cs" company="OpenTelemetry Authors">
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

#if NETCOREAPP3_1_OR_GREATER
using System;

namespace OpenTelemetry.Instrumentation.Runtime.Options
{
    /// <summary>
    /// Enum for Threading related metrics.
    /// </summary>
    [Flags]
    public enum ThreadingMetricOptions : int
    {
        /// <summary>
        /// Do not set flag for any metrics.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Set flag for all options.
        /// </summary>
        All = MonitorLockContentionCount | ThreadPoolThreadCount | ThreadPoolCompletedWorkItemCount | ThreadPoolPendingWorkItemCount | ActiveTimerCount,

        /// <summary>
        /// Set flag for whether to collect metrics for the number of times there was contention when trying to take the monitor's lock.
        /// </summary>
        MonitorLockContentionCount = 0b1,

        /// <summary>
        /// Set flag for whether to collect metrics for the number of thread pool threads that currently exist.
        /// </summary>
        ThreadPoolThreadCount = 0b10,

        /// <summary>
        /// Set flag for whether to collect metrics for the number of work items that have been processed so far.
        /// </summary>
        ThreadPoolCompletedWorkItemCount = 0b100,

        /// <summary>
        /// Set flag for whether to collect metrics for the number of work items that are currently queued to be processed.
        /// </summary>
        ThreadPoolPendingWorkItemCount = 0b1000,

        /// <summary>
        /// Set flag for whether to collect metrics for the number of timers that are currently active.
        /// </summary>
        ActiveTimerCount = 0b10000,
    }
}
#endif
