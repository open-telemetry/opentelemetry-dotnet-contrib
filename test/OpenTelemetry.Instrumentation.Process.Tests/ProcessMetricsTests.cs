// <copyright file="ProcessMetricsTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.Process.Tests;

public class ProcessMetricsTests
{
    private const int MaxTimeToAllowForFlush = 10000;
    private const string MetricPrefix = "process.dotnet.";

    [Fact]
    public void ProcessMetricsAreCaptured()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddProcessInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.True(exportedItems.Count == 2);
        Assert.StartsWith(MetricPrefix, exportedItems[0].Name);
        Assert.StartsWith(MetricPrefix, exportedItems[1].Name);

        var metric1 = exportedItems.FirstOrDefault(i => i.Name == "process.dotnet.physical.memory.usage");
        Assert.NotNull(metric1);

        var metric2 = exportedItems.FirstOrDefault(i => i.Name == "process.dotnet.virtual.memory.usage");
        Assert.NotNull(metric2);
    }
}
