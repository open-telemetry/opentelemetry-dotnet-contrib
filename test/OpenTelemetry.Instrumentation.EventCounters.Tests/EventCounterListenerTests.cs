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
        private const int MaxTimeToAllowForFlush = 10000;

        [Fact]
        public void SystemMetricsAreCaptured()
        {
            var metricItems = new List<MetricSnapshot>();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddEventCounterMetrics(options =>
                 {
                     options.RefreshIntervalSecs = 1;
                 })
                 .AddInMemoryExporter(metricItems)
                .Build();

            Task.Delay(1500).Wait();
            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            Assert.True(metricItems.Count > 1);
        }

        [Fact]
        public void TestEventCounterMetricsAreCaptured()
        {
            const int refreshIntervalSeconds = 1;
            var metricItems = new List<MetricSnapshot>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddEventCounterMetrics(options =>
                 {
                     options.RefreshIntervalSecs = 1;
                 })
                 .AddInMemoryExporter(metricItems)
                 .Build();
            var expected = new[] { 100.3F, 200.1F };
            TestEventCounter.Log.SampleCounter1(expected[0]);
            TestEventCounter.Log.SampleCounter2(expected[1]);

            // Wait a little bit over the refresh interval seconds
            Task.Delay((refreshIntervalSeconds * 1000) + 300).Wait();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            var counter1 = metricItems.Find(m => m.Name == "mycountername1");
            var counter2 = metricItems.Find(m => m.Name == "mycountername2");
            Assert.NotNull(counter1);
            Assert.NotNull(counter2);
            Assert.Equal(MetricType.DoubleGauge, counter1.MetricType); // EventCounter CounterType is `Mean`

            Assert.Equal(expected[0], GetActualValue(counter1));
            Assert.Equal(expected[1], GetActualValue(counter2));
        }

        [Fact]
        public void TestIncrementingEventCounterMetricsAreCaptured()
        {
            const int refreshIntervalSeconds = 1;
            var metricItems = new List<MetricSnapshot>();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddEventCounterMetrics(options =>
                 {
                     options.RefreshIntervalSecs = 1;
                 })
                 .AddInMemoryExporter(metricItems)
                 .Build();

            TestIncrementingEventCounter.Log.SampleCounter1(1);
            TestIncrementingEventCounter.Log.SampleCounter1(1);
            TestIncrementingEventCounter.Log.SampleCounter1(1);

            // Wait a little bit over the refresh interval seconds
            Task.Delay((refreshIntervalSeconds * 1000) + 300).Wait();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            var counter = metricItems.Find(m => m.Name == TestIncrementingEventCounter.CounterName);
            Assert.NotNull(counter);
            Assert.Equal(MetricType.DoubleSum, counter.MetricType); // EventCounter CounterType is `Sum`
            Assert.Equal(3, GetActualValue(counter));
        }

        [Fact]
        public void TestPollingCounterMetricsAreCaptured()
        {
            var metricItems = new List<MetricSnapshot>();
            const int refreshIntervalSeconds = 1;

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddEventCounterMetrics(options =>
                 {
                     options.RefreshIntervalSecs = refreshIntervalSeconds;
                 })
                 .AddInMemoryExporter(metricItems)
                 .Build();

            int i = 0;
            TestPollingEventCounter.CreateSingleton(() => ++i * 10);

            var duration = (refreshIntervalSeconds * 2 * 1000) + 300; // Wait for two refresh intervals to call the valueProvider twice
            Task.Delay(duration).Wait();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            var pollingCounter = metricItems.Find(m => m.Name == TestPollingEventCounter.CounterName);
            Assert.NotNull(pollingCounter);
            Assert.Equal(MetricType.DoubleGauge, pollingCounter.MetricType); // Polling Counter is EventCounter CounterType of `Mean`

            var expected = i * 10; // The last recorded `Mean` value
            Assert.Equal(expected, GetActualValue(pollingCounter));
        }

        [Fact]
        public void TestIncrementingPollingCounterMetrics()
        {
            var metricItems = new List<MetricSnapshot>();
            const int refreshIntervalSeconds = 1;

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddEventCounterMetrics(options =>
                 {
                     options.RefreshIntervalSecs = refreshIntervalSeconds;
                 })
                 .AddInMemoryExporter(metricItems)
                 .Build();

            int i = 1;

            TestIncrementingPollingCounter.CreateSingleton(() => i++);

            var duration = (refreshIntervalSeconds * 2 * 1000) + 300; // Wait for two refresh intervals to call the valueProvider twice
            Task.Delay(duration).Wait();

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            var pollingCounter = metricItems.Find(m => m.Name == TestIncrementingPollingCounter.CounterName);
            Assert.NotNull(pollingCounter);
            Assert.Equal(MetricType.DoubleSum, pollingCounter.MetricType); // Polling Counter is EventCounter CounterType of `Sum`

            Assert.Equal(1, GetActualValue(pollingCounter));
        }

        /// <summary>
        /// Event Counters are always Sum or Mean and are always record with `float`.
        /// </summary>
        /// <param name="metricSnapshot">Metric to Aggregate.</param>
        /// <returns>The Aggregated value. </returns>
        private static double GetActualValue(MetricSnapshot metricSnapshot)
        {
            double sum = 0;
            foreach (var metricPoint in metricSnapshot.MetricPoints)
            {
                if (metricSnapshot.MetricType.IsSum())
                {
                    sum += metricPoint.GetSumDouble();
                }
                else
                {
                    sum += metricPoint.GetGaugeLastValueDouble();
                }
            }

            return sum;
        }
    }
}
