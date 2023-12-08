// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging;

public static class GenevaLoggingExtensions
{
    public static OpenTelemetryLoggerOptions AddGenevaLogExporter(this OpenTelemetryLoggerOptions options, Action<GenevaExporterOptions> configure)
    {
        Guard.ThrowIfNull(options);

        var genevaOptions = new GenevaExporterOptions();
        configure?.Invoke(genevaOptions);
        var exporter = new GenevaLogExporter(genevaOptions);
        if (exporter.IsUsingUnixDomainSocket)
        {
            return options.AddProcessor(new BatchLogRecordExportProcessor(exporter));
        }
        else
        {
            return options.AddProcessor(new ReentrantExportProcessor<LogRecord>(exporter));
        }
    }
}
