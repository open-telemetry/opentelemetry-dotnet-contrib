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

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Extensions.Enrichment;
using OpenTelemetry.Trace;

namespace Examples.Enrichment;

public static class Program
{
    public static void Main()
    {
        // Create an ActivitySource.
        using var myActivitySource = new ActivitySource("MyCompany.MyProduct.MyLibrary");

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()

            // Register MyService in the DI container.
            .ConfigureServices(services => services.AddSingleton<IMyService, MyService>())

            // Register the ActivitySource as usual.
            .AddSource("MyCompany.MyProduct.MyLibrary")

            // Register an enricher class.
            // Important: AddTraceEnricher() must be called before any exporeters.
            .AddTraceEnricher<MyTraceEnricher>()

            // Add Console exporter to see the output of this example.
            .AddConsoleExporter()
            .Build();

        // Create an Activity and add some tags to it.
        using var activity = myActivitySource.StartActivity("SayHello");
        activity?.SetTag("hello", "world");

        // Tags from the enricher class will be added automatically to the created Activity.
    }
}
