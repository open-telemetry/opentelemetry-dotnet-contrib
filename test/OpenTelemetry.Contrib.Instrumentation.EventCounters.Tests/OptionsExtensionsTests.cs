// <copyright file="OptionsExtensionsTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Tests
{
    public class OptionsExtensionsTests
    {
        public class AddEventSourceMethod : OptionsExtensionsTests
        {
            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void Throws_Exception_When_EventSourceName_Is_Empty(string name)
            {
                var options = new EventCountersOptions();

                Func<EventSourceOption> action = () => options.AddEventSource(name);

                Assert.Throws<ArgumentNullException>("eventSourceName", action);
            }

            [Fact]
            public void Throws_Exception_When_Options_Is_Null()
            {
                EventCountersOptions options = null;

                Func<EventSourceOption> action = () => options.AddEventSource("test");

                Assert.Throws<ArgumentNullException>("options", action);
            }

            [Fact]
            public void Throws_Exception_When_EventSource_Already_Exists()
            {
                var options = new EventCountersOptions();
                options.AddEventSource("TestSource");

                Func<EventSourceOption> action = () => options.AddEventSource("TestSource");

                Assert.Throws<ArgumentException>("eventSourceName", action);
            }
        }

        public class WithCountersMethod : OptionsExtensionsTests
        {
            [Fact]
            public void Adds_Unknown_Counters()
            {
                var options = new EventSourceOption();

                options.WithCounters("firstCounter", "secondCounter");

                Assert.Equal(2, options.EventCounters.Count);

                Assert.Equal("firstCounter", options.EventCounters[0].Name);
                Assert.Null(options.EventCounters[0].Description);
                Assert.Equal(InstrumentationType.LongCounter, options.EventCounters[0].Type);
                Assert.Null(options.EventCounters[0].MetricName);
            }
        }

        public class WithMethod : OptionsExtensionsTests
        {
            [Fact]
            public void Adds_Counter()
            {
                var options = new EventSourceOption();

                options.With("firstCounter", "counterDescription", InstrumentationType.DoubleGauge, "counterMetric");

                Assert.Single(options.EventCounters);

                Assert.Equal("firstCounter", options.EventCounters[0].Name);
                Assert.Equal("counterDescription", options.EventCounters[0].Description);
                Assert.Equal(InstrumentationType.DoubleGauge, options.EventCounters[0].Type);
                Assert.Equal("counterMetric", options.EventCounters[0].MetricName);
            }
        }
    }
}
