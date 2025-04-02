// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using Serilog.Configuration;

namespace Serilog;

/// <summary>
/// Contains Serilog extension methods.
/// </summary>
public static class OpenTelemetrySerilogExtensions
{
    /// <summary>
    /// Adds a sink to Serilog <see cref="LoggerConfiguration"/> which will
    /// write to OpenTelemetry.
    /// </summary>
    /// <param name="loggerConfiguration"><see
    /// cref="LoggerSinkConfiguration"/>.</param>
    /// <param name="loggerProvider"><see
    /// cref="OpenTelemetryLoggerProvider"/>.</param>
    /// <param name="options"><see cref="OpenTelemetrySerilogSinkOptions"/>.</param>
    /// <param name="disposeProvider">Controls whether or not the supplied
    /// <paramref name="loggerProvider"/> will be disposed when
    /// the logger is disposed. Default value: <see
    /// langword="false"/>.</param>
    /// <returns>Supplied <see cref="LoggerConfiguration"/> for chaining calls.</returns>
    public static LoggerConfiguration OpenTelemetry(
        this LoggerSinkConfiguration loggerConfiguration,
        LoggerProvider loggerProvider,
        OpenTelemetrySerilogSinkOptions? options = null,
        bool disposeProvider = false)
    {
        Guard.ThrowIfNull(loggerConfiguration);
        Guard.ThrowIfNull(loggerProvider);

#pragma warning disable CA2000 // Dispose objects before losing scope
        return loggerConfiguration.Sink(new OpenTelemetrySerilogSink(loggerProvider, options, disposeProvider));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    /// <summary>
    /// Enrich logger output with information regarding the <see
    /// cref="System.Diagnostics.Activity"/> at the time of logging, if any.
    /// </summary>
    /// <param name="loggerEnrichmentConfiguration"><see
    /// cref="LoggerEnrichmentConfiguration"/>.</param>
    /// <param name="options"><see cref="OpenTelemetrySerilogEnricherOptions"/>.</param>
    /// <returns>Associated <see cref="LoggerConfiguration"/> for chaining calls.</returns>
    public static LoggerConfiguration WithOpenTelemetry(
        this LoggerEnrichmentConfiguration loggerEnrichmentConfiguration,
        OpenTelemetrySerilogEnricherOptions? options = null)
    {
        Guard.ThrowIfNull(loggerEnrichmentConfiguration);

        return loggerEnrichmentConfiguration.With(new OpenTelemetrySerilogEnricher(options ?? new()));
    }
}
