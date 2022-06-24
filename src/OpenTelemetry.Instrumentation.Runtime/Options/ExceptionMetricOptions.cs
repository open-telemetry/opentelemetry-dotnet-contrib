// <copyright file="ExceptionMetricOptions.cs" company="OpenTelemetry Authors">
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
    /// Enum for exception related metrics.
    /// </summary>
    [Flags]
    public enum ExceptionMetricOptions : int
    {
        /// <summary>
        /// Do not set flag for any metrics.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Set flag for all options.
        /// </summary>
        All = Count,

        /// <summary>
        /// Set flag for whether to collect metrics for the number of exception thrown in managed code.
        /// </summary>
        Count = 0b1,
    }
}
