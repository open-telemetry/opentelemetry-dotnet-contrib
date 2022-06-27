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
using OpenTelemetry.Metrics;

public class Program
{
    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter(options =>
            {
                options.StartHttpListener = true;
                options.ScrapeResponseCacheDurationMilliseconds = 0;
            })
            .Build();

        // Most of the process.runtime.dotnet.gc.* metrics are only available after the GC finished at least one collection.
        GC.Collect(1);

        // The process.runtime.dotnet.exception.count metrics are only available after an exception has been thrown post OpenTelemetry.Instrumentation.Runtime initialization.
        try
        {
            throw new Exception("Oops!");
        }
        catch (Exception)
        {
            // swallow the exception
        }

        Console.WriteLine(".NET Runtime metrics are available at http://localhost:9464/metrics, press any key to exit...");
        Console.ReadKey(false);
    }
}
