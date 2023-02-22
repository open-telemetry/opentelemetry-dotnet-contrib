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

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.OneCollector;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods to register the OneCollector log exporter.
/// </summary>
public static class OneCollectorOpenTelemetryLoggerOptionsExtensions
{
    /*
    TODO: Enable this once logging supports DI/options binding.
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
    */

    /// <summary>
    /// Add OneCollector exporter to the <see
    /// cref="OpenTelemetryLoggerOptions"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="OneCollectorLogExporterBuilder"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for call
    /// chaining.</returns>
    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options,
        Action<OneCollectorLogExporterBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return AddOneCollectorExporter(options, instrumentationKey: null, configuration: null, configure);
    }

    /// <summary>
    /// Add OneCollector exporter to the <see
    /// cref="OpenTelemetryLoggerOptions"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <param name="instrumentationKey">OneCollector instrumentation key.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for call
    /// chaining.</returns>
    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options,
        string instrumentationKey)
    {
        Guard.ThrowIfNullOrWhitespace(instrumentationKey);

        return AddOneCollectorExporter(options, instrumentationKey, configuration: null, configure: null);
    }

    /// <summary>
    /// Add OneCollector exporter to the <see
    /// cref="OpenTelemetryLoggerOptions"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <param name="instrumentationKey">OneCollector instrumentation key.</param>
    /// <param name="configure">Callback action for configuring <see cref="OneCollectorLogExporterBuilder"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for call
    /// chaining.</returns>
    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options,
        string instrumentationKey,
        Action<OneCollectorLogExporterBuilder> configure)
    {
        Guard.ThrowIfNullOrWhitespace(instrumentationKey);

        return AddOneCollectorExporter(options, instrumentationKey, configuration: null, configure);
    }

    /// <summary>
    /// Add OneCollector exporter to the <see
    /// cref="OpenTelemetryLoggerOptions"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <param name="configuration">Configuration used to build <see cref="OneCollectorLogExporterOptions"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for call
    /// chaining.</returns>
    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options,
        IConfiguration configuration)
    {
        Guard.ThrowIfNull(configuration);

        return AddOneCollectorExporter(options, instrumentationKey: null, configuration, configure: null);
    }

    /// <summary>
    /// Add OneCollector exporter to the <see
    /// cref="OpenTelemetryLoggerOptions"/>.
    /// </summary>
    /// <param name="options"><see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <param name="configuration">Configuration used to build <see cref="OneCollectorLogExporterOptions"/>.</param>
    /// <param name="configure">Callback action for configuring <see cref="OneCollectorLogExporterBuilder"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for call
    /// chaining.</returns>
    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options,
        IConfiguration configuration,
        Action<OneCollectorLogExporterBuilder> configure)
    {
        Guard.ThrowIfNull(configuration);

        return AddOneCollectorExporter(options, instrumentationKey: null, configuration, configure);
    }

    internal static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options,
        string? instrumentationKey,
        IConfiguration? configuration,
        Action<OneCollectorLogExporterBuilder>? configure)
    {
        Guard.ThrowIfNull(options);

        var builder = configuration == null
            ? new OneCollectorLogExporterBuilder(instrumentationKey)
            : new OneCollectorLogExporterBuilder(configuration);

        configure?.Invoke(builder);

        var exporterOptions = builder.Options;

        var batchOptions = exporterOptions.BatchOptions;

#pragma warning disable CA2000 // Dispose objects before losing scope
        options.AddProcessor(
            new BatchLogRecordExportProcessor(
                new OneCollectorLogExporter(exporterOptions),
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize));
#pragma warning restore CA2000 // Dispose objects before losing scope

        return options;
    }
}
