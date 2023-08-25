// <copyright file="InfluxDBMetricsExporterOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.InfluxDB;

/// <summary>
/// InfluxDB exporter options.
/// </summary>
public class InfluxDBMetricsExporterOptions
{
    /// <summary>
    /// Gets or sets HTTP/S destination for line protocol.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets name of InfluxDB organization that owns the destination bucket.
    /// </summary>
    public string? Org { get; set; }

    /// <summary>
    /// Gets or sets the name of InfluxDB bucket to which signals will be written.
    /// </summary>
    public string? Bucket { get; set; }

    /// <summary>
    /// Gets or sets the authentication token for InfluxDB.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the chosen metrics schema to write.
    /// </summary>
    public MetricsSchema MetricsSchema { get; set; } = MetricsSchema.TelegrafPrometheusV1;

    /// <summary>
    /// Gets or sets the time to wait at most (milliseconds) with the write.
    /// </summary>
    public int FlushInterval { get; set; } = 1000;
}
