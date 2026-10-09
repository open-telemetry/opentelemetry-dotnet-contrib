// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Runtime;
using OpenTelemetry.Internal;
#if !NET9_0_OR_GREATER
using System.Reflection;
using System.Runtime.Versioning;
#endif

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    private const string DotNetRuntimeMeterName = "System.Runtime";

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

#if NET9_0_OR_GREATER
        // This assembly itself was built for net9.0+, so the BCL it is running
        // against is guaranteed to be net9.0+ too (an app can roll a runtime
        // forward, never backward). The built-in "System.Runtime" meter is
        // always available here.
        return builder.AddMeter(DotNetRuntimeMeterName);
#else
        // This assembly was built for a pre-net9.0 target (net462, netstandard2.0,
        // or net8.0), but the app consuming it may still be running on a net9.0+
        // runtime with a net9.0+ build of System.Diagnostics.DiagnosticSource,
        // which does populate the built-in "System.Runtime" meter. Checking our
        // own compile-time target or Environment.Version is not reliable here
        // (see https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/4926):
        // neither necessarily matches the target framework that the actually
        // loaded System.Diagnostics.DiagnosticSource assembly was built against.
        // Ask that assembly directly instead.
        if (BuiltInRuntimeMeterIsAvailable.Value)
        {
            return builder.AddMeter(DotNetRuntimeMeterName);
        }

        var options = new RuntimeInstrumentationOptions();
        configure?.Invoke(options);

        builder.AddMeter(RuntimeMetrics.MeterInstance.Name);
        return builder.AddInstrumentation(() => new RuntimeMetrics(options));
#endif
    }

#if !NET9_0_OR_GREATER
    private static class BuiltInRuntimeMeterIsAvailable
    {
        public static readonly bool Value = Detect();

        private static bool Detect()
        {
            var frameworkName = typeof(System.Diagnostics.Metrics.Meter).Assembly
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;

            if (string.IsNullOrEmpty(frameworkName))
            {
                return false;
            }

            try
            {
                return new FrameworkName(frameworkName).Version.Major >= 9;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
#endif
}
