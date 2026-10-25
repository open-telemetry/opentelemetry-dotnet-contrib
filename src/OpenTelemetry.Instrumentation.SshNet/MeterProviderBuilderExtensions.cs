// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.SshNet.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of SSH.NET instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables collection of metrics from SSH.NET clients wrapped with
    /// <c>WithOpenTelemetry()</c>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddSshNetInstrumentation(this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder.AddMeter(SshNetTelemetry.Meter.Name);
    }
}
