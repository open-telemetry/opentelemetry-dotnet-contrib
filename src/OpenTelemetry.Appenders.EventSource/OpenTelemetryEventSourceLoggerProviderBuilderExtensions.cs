// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods for registering OpenTelemetry EventSource utilities into logging services.
/// </summary>
public static class OpenTelemetryEventSourceLoggerProviderBuilderExtensions
{
    /// <summary>
    /// Registers an <see cref="EventListener"/> which will convert <see
    /// cref="EventSource"/> events into OpenTelemetry logs.
    /// </summary>
    /// <param name="builder"><see
    /// cref="LoggerProviderBuilder"/>.</param>
    /// <param name="shouldListenToFunc"><inheritdoc
    /// cref="AddEventSourceLogEmitter(LoggerProviderBuilder, Func{string,
    /// EventLevel?}, string?,
    /// Action{OpenTelemetryEventSourceLogEmitterOptions}?)"
    /// path="/param[@name='shouldListenToFunc']"/></param>
    /// <returns>Supplied <see cref="LoggerProviderBuilder"/> for
    /// chaining calls.</returns>
    public static LoggerProviderBuilder AddEventSourceLogEmitter(
        this LoggerProviderBuilder builder,
        Func<string, EventLevel?> shouldListenToFunc)
        => AddEventSourceLogEmitter(builder, shouldListenToFunc, name: null, configure: null);

    /// <summary>
    /// Registers an <see cref="EventListener"/> which will convert <see
    /// cref="EventSource"/> events into OpenTelemetry logs.
    /// </summary>
    /// <param name="builder"><see
    /// cref="LoggerProviderBuilder"/>.</param>
    /// <param name="shouldListenToFunc">Callback function used to decide if
    /// events should be captured for a given <see
    /// cref="EventSource.Name"/>. Return <see langword="null"/> if no
    /// events should be captured.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="OpenTelemetryEventSourceLogEmitterOptions"/>.</param>
    /// <returns>Supplied <see cref="LoggerProviderBuilder"/> for
    /// chaining calls.</returns>
    public static LoggerProviderBuilder AddEventSourceLogEmitter(
        this LoggerProviderBuilder builder,
        Func<string, EventLevel?> shouldListenToFunc,
        string? name,
        Action<OpenTelemetryEventSourceLogEmitterOptions>? configure)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(shouldListenToFunc);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(configure));
        }

        builder.AddInstrumentation((sp, provider) =>
            new OpenTelemetryEventSourceLogEmitter(
                provider,
                shouldListenToFunc,
                sp.GetRequiredService<IOptionsMonitor<OpenTelemetryEventSourceLogEmitterOptions>>().Get(name),
                disposeProvider: false));

        return builder;
    }
}
