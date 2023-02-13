// <copyright file="OneCollectorOpenTelemetryLoggerOptionsExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods to register the OneCollector log exporter.
/// </summary>
public static class OneCollectorOpenTelemetryLoggerOptionsExtensions
{
    /// <summary>
    /// Add OneCollector exporter to the <see
    /// cref="OpenTelemetryLoggerOptions"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for call
    /// chaining.</returns>
    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options)
        => AddOneCollectorExporter(options, _ => { });

    /// <summary>
    /// Add OneCollector exporter to the <see
    /// cref="OpenTelemetryLoggerOptions"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="OneCollectorLogExporterOptions"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for call
    /// chaining.</returns>
    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options,
        Action<OneCollectorLogExporterOptions> configure)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(configure);

        var logExporterOptions = new OneCollectorLogExporterOptions();

        configure?.Invoke(logExporterOptions);

        var batchOptions = logExporterOptions.BatchOptions;

#pragma warning disable CA2000 // Dispose objects before losing scope
        options.AddProcessor(
            new BatchLogRecordExportProcessor(
                new OneCollectorLogExporter(logExporterOptions),
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize));
#pragma warning restore CA2000 // Dispose objects before losing scope

        return options;
    }
}
