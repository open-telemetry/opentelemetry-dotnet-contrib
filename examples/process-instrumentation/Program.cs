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

using System;
using OpenTelemetry;
using OpenTelemetry.Metrics;

public class Program
{
    public static void Main()
    {
        //MyTest();
    }

    [Microsoft.Coyote.SystematicTesting.Test]
    public static void MyTest()
    {
        var exportedItemsA = new List<Metric>();
        var exportedItemsB = new List<Metric>();

        var tasks = new List<Task>()
        {
                Task.Run(() =>
                {
                    using var meterProviderA = Sdk.CreateMeterProviderBuilder()
                        .AddProcessInstrumentation()
                        .AddInMemoryExporter(exportedItemsA)
                        .Build();
                }),

                Task.Run(() =>
                {
                    try
                    {
                        Sdk.CreateMeterProviderBuilder()
                            .AddProcessInstrumentation()
                            .AddInMemoryExporter(exportedItemsB)
                            .Build();
                    }
                    catch (Exception ex)
                    {

                    }
                }),
        };

        Task.WaitAll();

        return;
    }
}
