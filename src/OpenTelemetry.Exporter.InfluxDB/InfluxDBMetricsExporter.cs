// <copyright file="InfluxDBMetricsExporter.cs" company="OpenTelemetry Authors">
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
using InfluxDB.Client;
using InfluxDB.Client.Writes;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.InfluxDB;

internal sealed class InfluxDBMetricsExporter : BaseExporter<Metric>
{
    private readonly IMetricsWriter writer;
    private readonly InfluxDBClient influxDbClient;
    private readonly WriteApi writeApi;

    public InfluxDBMetricsExporter(IMetricsWriter writer, InfluxDBClient influxDbClient, WriteApi writeApi)
    {
        this.writer = writer;
        this.influxDbClient = influxDbClient;
        this.writeApi = writeApi;

        this.writeApi.EventHandler += (_, args) =>
        {
            switch (args)
            {
                case WriteErrorEvent writeErrorEvent:
                    InfluxDBEventSource.Log.FailedToExport(writeErrorEvent.Exception.Message);
                    break;
            }
        };
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        try
        {
            foreach (var metric in batch)
            {
                this.writer.Write(metric, this.ParentProvider?.GetResource(), this.writeApi);
            }

            return ExportResult.Success;
        }
        catch (Exception exception)
        {
            InfluxDBEventSource.Log.FailedToExport(exception.Message);
            return ExportResult.Failure;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.writeApi.Dispose();
            this.influxDbClient.Dispose();
        }

        base.Dispose(disposing);
    }
}
