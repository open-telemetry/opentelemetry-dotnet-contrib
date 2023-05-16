// <copyright file="MetricsSchema.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.InfluxDB;

/// <summary>
/// The OpenTelemetry->InfluxDB conversion schema.
/// </summary>
public enum MetricsSchema
{
    /// <summary>
    /// No schema selected. Default schema will be used.
    /// </summary>
    None,

    /// <summary>
    /// Represents the Telegraf Prometheus V1 schema.
    /// This option converts OpenTelemetry metrics to the first version of the Telegraf Prometheus schema.
    /// - Telegraf/InfluxDB measurement per Prometheus metric
    /// - Fields `count`/`sum`/`gauge`/etc contain metric values.
    /// </summary>
    TelegrafPrometheusV1 = 1,

    /// <summary>
    /// Represents the Telegraf Prometheus V2 schema.
    /// This option converts OpenTelemetry metrics to the second version of the Telegraf Prometheus schema.
    /// - One measurement `prometheus`
    /// - Fields (Prometheus metric name) + `_` + `count`/`sum`/`gauge`/etc contain metric values.
    /// </summary>
    TelegrafPrometheusV2 = 2,
}
