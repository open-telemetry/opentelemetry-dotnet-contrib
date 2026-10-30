// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.SshNet.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of SSH.NET instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables collection of spans from SSH.NET clients wrapped with
    /// <c>WithOpenTelemetry()</c>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddSshNetInstrumentation(this TracerProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder.AddSource(SshNetTelemetry.ActivitySource.Name);
    }
}
