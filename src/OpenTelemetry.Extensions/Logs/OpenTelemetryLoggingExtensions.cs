// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Contains OpenTelemetry logging SDK extensions.
/// </summary>
public static class OpenTelemetryLoggingExtensions
{
    /// <summary>
    /// Adds a <see cref="LogRecord"/> processor to the OpenTelemetry <see
    /// cref="OpenTelemetryLoggerOptions"/> which will convert log messages
    /// into <see cref="ActivityEvent"/>s attached to the active <see
    /// cref="Activity"/> when the message is written.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configure"><see cref="LogToActivityEventConversionOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="loggerOptions"/> is <c>null</c>.</exception>
    public static OpenTelemetryLoggerOptions AttachLogsToActivityEvent(
        this OpenTelemetryLoggerOptions loggerOptions,
        Action<LogToActivityEventConversionOptions>? configure = null)
    {
        Guard.ThrowIfNull(loggerOptions);

        var options = new LogToActivityEventConversionOptions();
        configure?.Invoke(options);
#pragma warning disable CA2000 // Dispose objects before losing scope
        return loggerOptions.AddProcessor(new ActivityEventAttachingLogProcessor(options));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    /// <summary>
    /// Add trace ids to logs.
    /// </summary>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static ILoggingBuilder AddTraceId(this ILoggingBuilder builder)
    {
        var services = builder
            .Services;

        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType == typeof(ILoggerProvider))
            {
                var descriptor = services[i];

                services[i] = ServiceDescriptor.Describe(
                    typeof(ILoggerProvider),
                    provider =>
                    {
                        var loggerInstance = descriptor.ImplementationInstance is { } implementationInstance
                            ? implementationInstance
                            : descriptor.ImplementationType is { } implementationType
                                ? ActivatorUtilities.CreateInstance(provider, implementationType)
                                : descriptor.ImplementationFactory!(provider);

                        return new AddTraceIdLoggerProvider((ILoggerProvider)loggerInstance);
                    },
                    descriptor.Lifetime);
            }
        }

        return builder;
    }
}
