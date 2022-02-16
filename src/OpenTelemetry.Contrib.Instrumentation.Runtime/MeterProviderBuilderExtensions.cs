// <copyright file="MeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Contrib.Instrumentation.Runtime;
using OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering of dependency instrumentation.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Enables runtime instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <param name="configure">Runtime metrics options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddRuntimeMetrics(
            this MeterProviderBuilder builder,
            Action<RuntimeMetricsOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddRuntimeMetrics(builder, sp.GetOptions<RuntimeMetricsOptions>(), configure);
                });
            }

            return AddRuntimeMetrics(builder, new RuntimeMetricsOptions(), configure);
        }

        private static MeterProviderBuilder AddRuntimeMetrics(
            MeterProviderBuilder builder,
            RuntimeMetricsOptions options,
            Action<RuntimeMetricsOptions> configure)
        {
            configure?.Invoke(options);

            var instrumentation = new RuntimeMetrics(options);
            builder.AddMeter(RuntimeMetrics.InstrumentationName);
            return builder.AddInstrumentation(() => instrumentation);
        }
    }
}
