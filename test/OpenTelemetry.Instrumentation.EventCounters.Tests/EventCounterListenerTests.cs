// <copyright file="EventCounterListenerTests.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests
{
    public class EventCounterListenerTests
    {
        public class EventCountersInstrumentationTests
        {
            private const int MaxTimeToAllowForFlush = 10000;
            private MeterProvider meterProvider = null;

            [Fact]
            public async Task MetricsAreCaptured()
            {
                var metricItems = new List<Metric>();

                this.meterProvider = Sdk.CreateMeterProviderBuilder()
                     .AddEventCounterMetrics(options =>
                     {
                         options.RefreshIntervalSecs = 1;
                     })
                     .AddInMemoryExporter(metricItems)
                    .Build();

                await Task.Delay(2000);
                this.meterProvider.ForceFlush(MaxTimeToAllowForFlush);

                this.meterProvider.Dispose();

                Assert.True(metricItems.Count > 1);
            }
        }
    }
}
