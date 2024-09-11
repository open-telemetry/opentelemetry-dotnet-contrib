// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Instrumentation.SqlClient;

public static class SqlClientMeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds SqlClient instrumentation to the meter provider.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddSqlClientInstrumentation(
        this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        var name = Options.DefaultName;

#if NET6_0_OR_GREATER

        builder.AddInstrumentation(sp =>
        {
            var sqlOptions = sp.GetRequiredService<IOptionsMonitor<SqlClientTraceInstrumentationOptions>>().Get(name);

            return new SqlClientInstrumentation(sqlOptions);
        });

        builder.AddMeter(SqlActivitySourceHelper.MeterName);
#endif

        return builder;
    }
}
