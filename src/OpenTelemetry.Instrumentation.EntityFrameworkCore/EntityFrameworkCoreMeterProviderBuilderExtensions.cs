// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Instrumentation.EntityFrameworkCore;
using OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class EntityFrameworkCoreMeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables EntityFrameworkCore instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
#if NET
    // TODO [RequiresUnreferencedCode(SqlClientInstrumentation.SqlClientTrimmingUnsupportedMessage)]
#endif
    public static MeterProviderBuilder AddEntityFrameworkCoreInstrumentation(this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        builder.AddInstrumentation(sp =>
        {
            return EntityFrameworkInstrumentation.AddMetricHandle();
        });

        builder.AddMeter(EntityFrameworkDiagnosticListener.MeterName);

        return builder;
    }
}
