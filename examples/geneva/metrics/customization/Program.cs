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

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Metrics;

namespace customization;

public class Program
{
    private static readonly Meter MyMeter = new Meter("MyCompany.MyProduct.MyLibrary", "1.0");
    private static readonly Histogram<long> LatencyHistogram = MyMeter.CreateHistogram<long>("LatencyHistogram");

    public static void Main(string[] args)
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MyMeter.Name)
            .AddView(LatencyHistogram.Name, new ExplicitBucketHistogramConfiguration { Boundaries = new double[] { 25, 50, 75, 100, 150, 200 } })
            .AddGenevaMetricExporter(options =>
            {
                options.ConnectionString = "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace";

                // Emit the following dimensions for every metric exported by this instance of GenevaMetricExporter
                options.PrepopulatedMetricDimensions = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                };
            })
            .Build();

        // This could be a sequence of Histogram measurements recorded across your application.
        LatencyHistogram.Record(100, new("verb", "GET"), new("statusCode", 200));
        LatencyHistogram.Record(115, new("verb", "GET"), new("statusCode", 200));
        LatencyHistogram.Record(105, new("verb", "GET"), new("statusCode", 200));
        LatencyHistogram.Record(85, new("verb", "GET"), new("statusCode", 200));
        LatencyHistogram.Record(90, new("verb", "GET"), new("statusCode", 200));
        LatencyHistogram.Record(50, new("verb", "GET"), new("statusCode", 401));

        LatencyHistogram.Record(150, new("verb", "POST"), new("statusCode", 200));
        LatencyHistogram.Record(175, new("verb", "POST"), new("statusCode", 200));
        LatencyHistogram.Record(165, new("verb", "POST"), new("statusCode", 200));
        LatencyHistogram.Record(255, new("verb", "POST"), new("statusCode", 200));
        LatencyHistogram.Record(154, new("verb", "POST"), new("statusCode", 200));
        LatencyHistogram.Record(75, new("verb", "POST"), new("statusCode", 503));

        LatencyHistogram.Record(120, new("verb", "PUT"), new("statusCode", 200));
        LatencyHistogram.Record(125, new("verb", "PUT"), new("statusCode", 200));
        LatencyHistogram.Record(115, new("verb", "PUT"), new("statusCode", 200));
    }
}
