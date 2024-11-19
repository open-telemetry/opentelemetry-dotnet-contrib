// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET9_0_OR_GREATER
using OpenTelemetry.Instrumentation.Runtime;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
#if NET9_0_OR_GREATER
    private const string DotNetRuntimeMeterName = "System.Runtime";
#endif

    /// <summary>
    /// Enables runtime instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddRuntimeInstrumentation(
        this MeterProviderBuilder builder) =>
#if NET9_0_OR_GREATER
        builder.AddMeter(DotNetRuntimeMeterName);
#else
        AddRuntimeInstrumentation(builder, configure: null);
#endif

#if !NET9_0_OR_GREATER
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
#endif
}
