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
using OpenTelemetry.Instrumentation.EventCounters;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables EventCounter instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">Runtime metrics options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddEventCounterMetrics(
        this MeterProviderBuilder builder,
        Action<EventCounterMetricsOptions> configure = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new EventCounterMetricsOptions();
        configure?.Invoke(options);

        var instrumentation = new EventCounterListener(options);
        builder.AddMeter(EventCounterListener.InstrumentationName);
        return builder.AddInstrumentation(() => instrumentation);
    }
}
