// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry;
using OpenTelemetry.Exporter.InfluxDB;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

Action<ResourceBuilder> configureResource = r => r.AddService(
    serviceName: "influx-exporter-test",
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    serviceInstanceId: Environment.MachineName);

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(configureResource)
    .AddRuntimeInstrumentation()
    .AddInfluxDBMetricsExporter(options =>
    {
        options.Org = "org";
        options.Bucket = "bucket";
        options.Token = "token";
        options.Endpoint = new Uri("http://localhost:8086");
        options.MetricsSchema = MetricsSchema.TelegrafPrometheusV2;
    })
    .Build();

meterProvider.ForceFlush();

await Task.Delay(TimeSpan.FromSeconds(10));
