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

using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

public static class OneCollectorOpenTelemetryLoggerOptionsExtensions
{
    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(this OpenTelemetryLoggerOptions options)
        => AddOneCollectorExporter(options, _ => { });

    public static OpenTelemetryLoggerOptions AddOneCollectorExporter(
        this OpenTelemetryLoggerOptions options,
        Action<OneCollectorLogExporterOptions> configure)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(configure);

        var logExporterOptions = new OneCollectorLogExporterOptions();

        configure?.Invoke(logExporterOptions);

        var exporter = new OneCollectorExporter<LogRecord>(
            logExporterOptions);

        var batchOptions = logExporterOptions.BatchOptions;

        options.AddProcessor(
            new BatchLogRecordExportProcessor(
                exporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize));

        return options;
    }
}
