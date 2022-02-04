// <copyright file="EventCountersOptionsTests.cs" company="OpenTelemetry Authors">
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

using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Tests
{
    public class EventCountersOptionsTests
    {
        [Fact]
        public void Can_Be_Read_From_Configuration()
        {
            var json = @"{
            ""Telemetry"": {
                ""RefreshIntervalSecs"": 55,
                ""Sources"": [
                    {
                        ""EventSourceName"": ""System.Runtime"",
                        ""EventCounters"" : [
                            {
                                ""Name"": ""cpu-usage"",
                                ""Description"": ""Current CPU usage"",
                                ""Type"": ""DoubleGauge""
                            },
                             {
                                ""Name"": ""working-set"",
                                ""Description"": ""Process working set"",
                                ""Type"": ""Counter"",
                                ""MetricName"": ""process_working_set""
                            }
                        ]
                    },
                    {
                        ""EventSourceName"": ""MyCustomSource"",
                        ""EventCounters"" : [
                            {
                                ""Name"": ""orders_submitted"",
                                ""Description"": ""Number of submitted orders"",
                                ""Type"": ""Counter""
                            }
                        ]
                    }
                ]
            }
}";

            var configuration = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();

            var options = configuration.GetSection("Telemetry").Get<EventCountersOptions>();
            Assert.Equal(55, options.RefreshIntervalSecs);
            Assert.Equal(2, options.Sources.Count);

            Assert.Equal(2, options.Sources[0].EventCounters.Count);
            Assert.Equal("System.Runtime", options.Sources[0].EventSourceName);

            Assert.Equal("cpu-usage", options.Sources[0].EventCounters[0].Name);
            Assert.Equal("Current CPU usage", options.Sources[0].EventCounters[0].Description);
            Assert.Equal(InstrumentationType.DoubleGauge, options.Sources[0].EventCounters[0].Type);

            Assert.Equal("working-set", options.Sources[0].EventCounters[1].Name);
            Assert.Equal("Process working set", options.Sources[0].EventCounters[1].Description);
            Assert.Equal(InstrumentationType.Counter, options.Sources[1].EventCounters[0].Type);
            Assert.Equal("process_working_set", options.Sources[0].EventCounters[1].MetricName);
        }
    }
}
