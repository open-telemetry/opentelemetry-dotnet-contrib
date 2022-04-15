// <copyright file="GenevaMetricExporterExtensions.cs" company="OpenTelemetry Authors">
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

using System;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva;

public static class GenevaMetricExporterExtensions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
    public static MeterProviderBuilder AddGenevaMetricExporter(this MeterProviderBuilder builder, Action<GenevaMetricExporterOptions> configure = null)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var options = new GenevaMetricExporterOptions();
        configure?.Invoke(options);
        return builder.AddReader(new PeriodicExportingMetricReader(new GenevaMetricExporter(options), options.MetricExportIntervalMilliseconds));
    }
}
