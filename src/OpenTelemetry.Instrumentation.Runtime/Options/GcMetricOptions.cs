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

using System;

namespace OpenTelemetry.Instrumentation.Runtime.Options
{
    /// <summary>
    /// Enum for garbage collection metrics.
    /// </summary>
    [Flags]
    public enum GcMetricOptions : int
    {
        /// <summary>
        /// Do not set flag for any metrics.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Set flag for all options.
        /// </summary>
        All = Count
#if NETCOREAPP3_1_OR_GREATER
            | AllocatedBytes
#endif

#if NET6_0_OR_GREATER
            | CommitedBytes | HeapSize | FragmentationSize
#endif
            ,

        /// <summary>
        /// Set flag for whether to collect metrics for garbage collection count.
        /// </summary>
        Count = 0b1,

#if NETCOREAPP3_1_OR_GREATER
        /// <summary>
        /// Set flag for whether to collect metrics for bytes allocated over the lifetime of the process.
        /// </summary>
        AllocatedBytes = 0b10,
#endif

#if NET6_0_OR_GREATER
        /// <summary>
        /// Set flag for whether to collect metrics for commited bytes.
        /// </summary>
        CommitedBytes = 0b100,

        /// <summary>
        /// Set flag for whether to collect metrics for heap size for each generation.
        /// </summary>
        HeapSize = 0b1000,

        /// <summary>
        /// Set flag for whether to collect metrics for fragmentation size.
        /// </summary>
        FragmentationSize = 0b10000,
#endif
    }
}
