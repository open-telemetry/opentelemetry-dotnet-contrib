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
using OpenTelemetry.Contrib.Instrumentation.EventCounters;
using OpenTelemetry.Contrib.Instrumentation.EventCounters.Implementation;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering of dependency instrumentation.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Enables EventCounters instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
        /// <param name="configureEventCounterListenerOptions">EventCounter Listener configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddEventCounters(
            this MeterProviderBuilder builder,
            Action<EventCountersOptions> configureEventCounterListenerOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var options = new EventCountersOptions();
            configureEventCounterListenerOptions?.Invoke(options);
            builder.AddMeter(MeterTelemetryPublisher.InstrumentationName);
            builder.AddInstrumentation(() => new EventCounterListenerInstrumentation(options));

            return builder;
        }
    }
}
