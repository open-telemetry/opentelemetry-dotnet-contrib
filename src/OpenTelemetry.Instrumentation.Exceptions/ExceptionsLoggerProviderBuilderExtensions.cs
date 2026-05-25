// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.Exceptions.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Extension methods to simplify registering unhandled exception instrumentation.
/// </summary>
public static class ExceptionsLoggerProviderBuilderExtensions
{
    /// <summary>
    /// Adds unhandled exception instrumentation to the logger provider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddExceptionsInstrumentation(this LoggerProviderBuilder builder) =>
        AddExceptionsInstrumentation(builder, configure: null);

    /// <summary>
    /// Adds unhandled exception instrumentation to the logger provider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback action for configuring <see cref="ExceptionsInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddExceptionsInstrumentation(
        this LoggerProviderBuilder builder,
        Action<ExceptionsInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        var options = new ExceptionsInstrumentationOptions();
        configure?.Invoke(options);

        return builder.AddProcessor(sp => new ExceptionsInstrumentationLogProcessor(
            sp.GetRequiredService<ILoggerFactory>(),
            options));
    }

    /// <summary>
    /// Adds unhandled exception instrumentation to the OpenTelemetry logger options.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/> being configured.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddExceptionsInstrumentation(this OpenTelemetryLoggerOptions options) =>
        AddExceptionsInstrumentation(options, configure: null);

    /// <summary>
    /// Adds unhandled exception instrumentation to the OpenTelemetry logger options.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/> being configured.</param>
    /// <param name="configure">Callback action for configuring <see cref="ExceptionsInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddExceptionsInstrumentation(
        this OpenTelemetryLoggerOptions options,
        Action<ExceptionsInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(options);

        var instrumentationOptions = new ExceptionsInstrumentationOptions();
        configure?.Invoke(instrumentationOptions);

        return options.AddProcessor(sp => new ExceptionsInstrumentationLogProcessor(
            sp.GetRequiredService<ILoggerFactory>(),
            instrumentationOptions));
    }
}
