// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Runtime;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables runtime instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddRuntimeInstrumentation(
        this MeterProviderBuilder builder) =>
        AddRuntimeInstrumentation(builder, configure: null);

    /// <summary>
    /// Enables runtime instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">Runtime metrics options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddRuntimeInstrumentation(
        this MeterProviderBuilder builder,
        Action<RuntimeInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        var options = new RuntimeInstrumentationOptions();
        configure?.Invoke(options);

        var instrumentation = new RuntimeMetrics(options);
        builder.AddMeter(RuntimeMetrics.MeterInstance.Name);
        return builder.AddInstrumentation(() => instrumentation);
    }
}
