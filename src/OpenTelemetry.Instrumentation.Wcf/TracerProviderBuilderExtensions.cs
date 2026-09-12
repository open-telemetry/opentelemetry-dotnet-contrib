// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Wcf;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables the outgoing requests automatic data collection for WCF.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilderExtensions"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilderExtensions"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddWcfInstrumentation(this TracerProviderBuilder builder) =>
        AddWcfInstrumentation(builder, configure: null);

    /// <summary>
    /// Enables the outgoing requests automatic data collection for WCF.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilderExtensions"/> being configured.</param>
    /// <param name="configure">Wcf configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilderExtensions"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddWcfInstrumentation(this TracerProviderBuilder builder, Action<WcfInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        if (WcfInstrumentationActivitySource.Options != null)
        {
            throw new NotSupportedException("WCF instrumentation has already been registered and doesn't support multiple registrations.");
        }

        var options = new WcfInstrumentationOptions();
        configure?.Invoke(options);

        WcfInstrumentationActivitySource.Options = options;

#if NETFRAMEWORK
        Instrumentation.Wcf.Implementation.AspNetParentSpanCorrector.Register();
#endif

        builder.AddInstrumentation(() => new WcfInstrumentation());

        return builder.AddSource(WcfInstrumentationActivitySource.ActivitySource.Name);
    }

    /// <summary>
    /// Tracks the lifetime of a WCF instrumentation registration so that the
    /// static <see cref="WcfInstrumentationActivitySource.Options"/> can be
    /// cleared when the owning <see cref="TracerProvider"/> is disposed.
    /// </summary>
    private sealed class WcfInstrumentation : IDisposable
    {
        public void Dispose() => WcfInstrumentationActivitySource.Options = null;
    }
}
