// <copyright file="InstrumentationType.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters
{
    /// <summary>
    /// Defines the type of the created instrumentation.
    /// </summary>
    public enum InstrumentationType
    {
        /// <summary>
        /// Creates a counter instrumentation of long values which is an instrument that reports monotonically
        /// increasing values.
        /// </summary>
        LongCounter = 0,

        /// <summary>
        /// Creates a counter instrumentation of double values which is an instrument that reports monotonically
        /// increasing values.
        /// </summary>
        DoubleCounter = 1,

        /// <summary>
        /// Creates a gauge instrumentation of long values which is an instrument that reports
        /// non-additive values. An example of a non-additive
        /// value is the room temperature - it makes no sense to report the temperature value
        /// from multiple rooms and sum them up.
        /// </summary>
        LongGauge = 2,

        /// <summary>
        /// Creates a gauge instrumentation of double values which is an instrument that reports
        /// non-additive values. An example of a non-additive
        /// value is the room temperature - it makes no sense to report the temperature value
        /// from multiple rooms and sum them up.
        /// </summary>
        DoubleGauge = 3,
    }
}
