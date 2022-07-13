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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests
{
    public class EventCounterListenerTests
    {
        private const int MaxTimeToAllowForFlush = 10000;
        private const int MaxRetries = 30;
        private MeterProvider meterProvider;

        [Fact]
        public async Task SystemMetricsAreCaptured()
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

        [Fact]
        public void TestEventCounterMetricsAreCaptured()
        {
            const int refreshIntervalSeconds = 1;
            var metricItems = new List<Metric>();
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
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
            Task.Delay(refreshIntervalSeconds * 1000).Wait();

            var retries = MaxRetries;
            Metric counter1 = null, counter2 = null;
            while (retries-- > 0)
            {
                this.meterProvider.ForceFlush(MaxTimeToAllowForFlush);
                counter1 = metricItems.Find(m => m.Name == "mycountername1");
                counter2 = metricItems.Find(m => m.Name == "mycountername2");

                if (counter1 != null && GetActualValue(counter1) > 0)
                {
                    break;
                }

                Task.Delay(100).Wait();
            }

            this.meterProvider.Dispose();
            Assert.NotNull(counter1);
            Assert.NotNull(counter2);
            Assert.Equal(MetricType.DoubleGauge, counter1.MetricType); // EventCounter CounterType is `Mean`

            Assert.True(Math.Abs(expected[0] - GetActualValue(counter1)) < .001, $"Expected value doesn't match after {MaxRetries - retries}");
            Assert.Equal(expected[1], GetActualValue(counter2));
        }

        [Fact]
        public void TestIncrementingEventCounterMetricsAreCaptured()
        {
            const int refreshIntervalSeconds = 1;
            var metricItems = new List<Metric>();
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddEventCounterMetrics(options =>
                 {
                     options.RefreshIntervalSecs = 1;
                 })
                 .AddInMemoryExporter(metricItems)
                 .Build();

            TestIncrementingEventCounter.Log.SampleCounter1(1);
            TestIncrementingEventCounter.Log.SampleCounter1(1);
            TestIncrementingEventCounter.Log.SampleCounter1(1);

            Task.Delay(refreshIntervalSeconds * 1000).Wait();
            var retries = MaxRetries;
            Metric counter = null;

            while (retries-- > 0)
            {
                this.meterProvider.ForceFlush(MaxTimeToAllowForFlush);
                counter = metricItems.Find(m => m.Name == TestIncrementingEventCounter.CounterName);
                if (counter != null && GetActualValue(counter) > 0)
                {
                    break;
                }

                Task.Delay(100).Wait();
            }

            this.meterProvider.Dispose();

            Assert.NotNull(counter);
            Assert.Equal(MetricType.DoubleSum, counter.MetricType); // EventCounter CounterType is `Sum`
            Assert.Equal(3, GetActualValue(counter));
        }

        [Fact]
        public void TestPollingCounterMetricsAreCaptured()
        {
            var metricItems = new List<Metric>();
            const int refreshIntervalSeconds = 1;

            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddEventCounterMetrics(options =>
                 {
                     options.RefreshIntervalSecs = refreshIntervalSeconds;
                 })
                 .AddInMemoryExporter(metricItems)
                 .Build();

            int i = 0;
            TestPollingEventCounter.CreateSingleton(() => ++i * 10);

            var duration = refreshIntervalSeconds * 2 * 1000; // Wait for two refresh intervals to call the valueProvider twice
            Task.Delay(duration).Wait();

            var retries = MaxRetries;
            Metric pollingCounter = null;
            while (retries-- > 0)
            {
                this.meterProvider.ForceFlush(MaxTimeToAllowForFlush);
                pollingCounter = metricItems.Find(m => m.Name == TestPollingEventCounter.CounterName);
                if (pollingCounter != null && GetActualValue(pollingCounter) > 0)
                {
                    break;
                }

                Task.Delay(100).Wait();
            }

            this.meterProvider.Dispose();

            Assert.NotNull(pollingCounter);
            Assert.Equal(MetricType.DoubleGauge, pollingCounter.MetricType); // Polling Counter is EventCounter CounterType of `Mean`

            var expected = i * 10; // The last recorded `Mean` value
            Assert.Equal(expected, GetActualValue(pollingCounter));
        }

        [Fact]
        public async Task TestIncrementingPollingCounterMetrics()
        {
            var metricItems = new List<Metric>();
            const int refreshIntervalSeconds = 1;

            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                 .AddEventCounterMetrics(options =>
                 {
                     options.RefreshIntervalSecs = refreshIntervalSeconds;
                 })
                 .AddInMemoryExporter(metricItems)
                 .Build();

            int i = 1;

            TestIncrementingPollingCounter.CreateSingleton(() => i++);

            var duration = refreshIntervalSeconds * 2 * 1000; // Wait for two refresh intervals to call the valueProvider twice
            await Task.Delay(duration);

            var retries = MaxRetries;
            Metric pollingCounter = null;
            while (retries-- > 0)
            {
                this.meterProvider.ForceFlush(MaxTimeToAllowForFlush);
                pollingCounter = metricItems.Find(m => m.Name == TestIncrementingPollingCounter.CounterName);
                if (pollingCounter != null && GetActualValue(pollingCounter) > 0)
                {
                    break;
                }

                Task.Delay(100).Wait();
            }

            this.meterProvider.Dispose();

            Assert.NotNull(pollingCounter);
            Assert.Equal(MetricType.DoubleSum, pollingCounter.MetricType); // Polling Counter is EventCounter CounterType of `Sum`

            Assert.Equal(1, GetActualValue(pollingCounter));
        }

        /// <summary>
        /// Event Counters are always Sum or Mean and are always record with `float`.
        /// </summary>
        /// <param name="metric">Metric to Aggregate.</param>
        /// <returns>The Aggregated value. </returns>
        private static double GetActualValue(Metric metric)
        {
            double sum = 0;
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                if (metric.MetricType.IsSum())
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
