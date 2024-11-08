// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class SqlClientMeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables SqlClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
#if NET
    [RequiresUnreferencedCode(SqlClientInstrumentation.SqlClientTrimmingUnsupportedMessage)]
#endif
    public static MeterProviderBuilder AddSqlClientInstrumentation(this MeterProviderBuilder builder)
        => AddSqlClientInstrumentation(builder, name: null, configureSqlClientMetricsInstrumentationOptions: null);

    /// <summary>
    /// Enables SqlClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configureSqlClientMetricsInstrumentationOptions">Callback action for configuring <see cref="SqlClientMetricsInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
#if NET
    [RequiresUnreferencedCode(SqlClientInstrumentation.SqlClientTrimmingUnsupportedMessage)]
#endif
    public static MeterProviderBuilder AddSqlClientInstrumentation(
        this MeterProviderBuilder builder,
        Action<SqlClientMetricsInstrumentationOptions> configureSqlClientMetricsInstrumentationOptions)
        => AddSqlClientInstrumentation(builder, name: null, configureSqlClientMetricsInstrumentationOptions);

    /// <summary>
    /// Enables SqlClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureSqlClientMetricsInstrumentationOptions">Callback action for configuring <see cref="SqlClientMetricsInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
#if NET
    [RequiresUnreferencedCode(SqlClientInstrumentation.SqlClientTrimmingUnsupportedMessage)]
#endif
    public static MeterProviderBuilder AddSqlClientInstrumentation(
        this MeterProviderBuilder builder,
        string? name,
        Action<SqlClientMetricsInstrumentationOptions>? configureSqlClientMetricsInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configureSqlClientMetricsInstrumentationOptions != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureSqlClientMetricsInstrumentationOptions));
        }

        builder.AddInstrumentation(sp =>
        {
            var sqlOptions = sp.GetRequiredService<IOptionsMonitor<SqlClientMetricsInstrumentationOptions>>().Get(name);
            SqlClientInstrumentation.MetricOptions = sqlOptions;
            return SqlClientInstrumentation.AddMetricHandle();
        });

        builder.AddMeter(SqlActivitySourceHelper.MeterName);

        return builder;
    }
}
