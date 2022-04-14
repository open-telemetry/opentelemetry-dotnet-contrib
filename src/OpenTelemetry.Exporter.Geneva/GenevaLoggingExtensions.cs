// <copyright file="GenevaLoggingExtensions.cs" company="OpenTelemetry Authors">
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

#if NETSTANDARD2_0 || NET461
using System;
using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging;

public static class GenevaLoggingExtensions
{
    public static OpenTelemetryLoggerOptions AddGenevaLogExporter(this OpenTelemetryLoggerOptions options, Action<GenevaExporterOptions> configure)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

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
#endif
