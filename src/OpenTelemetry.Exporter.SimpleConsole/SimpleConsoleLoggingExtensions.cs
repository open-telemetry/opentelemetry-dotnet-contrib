// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Exporter.SimpleConsole;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods to register the SimpleConsole log exporter.
/// </summary>
public static class SimpleConsoleLoggingExtensions
{
    /// <summary>
    /// Add SimpleConsole exporter to the <see cref="OpenTelemetryLoggerOptions"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="SimpleConsoleExporterOptions"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for call chaining.</returns>
    public static OpenTelemetryLoggerOptions AddSimpleConsoleExporter(
        this OpenTelemetryLoggerOptions options,
        Action<SimpleConsoleExporterOptions>? configure = null)
    {
        Guard.ThrowIfNull(options);

        var exporterOptions = new SimpleConsoleExporterOptions();
        configure?.Invoke(exporterOptions);

        return options.AddProcessor(sp => new SimpleLogRecordExportProcessor(new SimpleConsoleExporter(exporterOptions)));
    }
}
