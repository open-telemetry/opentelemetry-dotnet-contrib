// <copyright file="GrpcOptionsExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Contrib.Instrumentation.EventCounters.Grpc;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods for the event counters options.
    /// </summary>
    public static class GrpcOptionsExtensions
    {
        /// <summary>
        /// Adds the Grpc event source.
        /// </summary>
        /// <param name="options">The options to add the event source to.</param>
        /// <returns>The options instance.</returns>
        public static IGrpcBuilder AddGrcpServer(this EventCountersOptions options)
        {
            return new GrpcBuilder(options);
        }
    }
}
