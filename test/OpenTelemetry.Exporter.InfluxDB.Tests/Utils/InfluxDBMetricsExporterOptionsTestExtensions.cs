// <copyright file="InfluxDBMetricsExporterOptionsTestExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.InfluxDB.Tests.Utils;

public static class InfluxDBMetricsExporterOptionsTestExtensions
{
    public static void WithDefaultTestConfiguration(this InfluxDBMetricsExporterOptions options)
    {
        options.Bucket = "MyBucket";
        options.Org = "MyOrg";
        options.Token = "MyToken";

        // For tests we want to flush the metrics ASAP
        options.FlushInterval = 1;
    }
}
