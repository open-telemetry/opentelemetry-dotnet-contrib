// <copyright file="ThreadingInstrumentation.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Metrics;
using System.Threading;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    internal class ThreadingInstrumentation : IRuntimeInstrumentation
    {
        private readonly Meter meter;
        private readonly ObservableCounter<int> threadPoolThreadCounter;
        private readonly ObservableGauge<long> monitorContentionCounter;
        private readonly ObservableCounter<long> threadPoolQueueCounter;
        private readonly ObservableGauge<long> completedItemsCounter;
        private readonly ObservableCounter<long> timerCounter;

        public ThreadingInstrumentation(Meter meter)
        {
            this.meter = meter;
            this.threadPoolThreadCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}threadpool_thread_count", () => ThreadPool.ThreadCount, description: "ThreadPool Thread Count");
            this.monitorContentionCounter = meter.CreateObservableGauge($"{RuntimeMetrics.MetricPrefix}monitor_lock_contention_count", () => Monitor.LockContentionCount, description: "Monitor Lock Contention Count");
            this.threadPoolQueueCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}threadpool_queue_length", () => ThreadPool.PendingWorkItemCount, description: "ThreadPool Queue Length");
            this.completedItemsCounter = meter.CreateObservableGauge($"{RuntimeMetrics.MetricPrefix}threadpool_completed_items_count", () => ThreadPool.CompletedWorkItemCount, description: "ThreadPool Completed Work Item Count");
            this.timerCounter = meter.CreateObservableCounter($"{RuntimeMetrics.MetricPrefix}active_timer_count", () => Timer.ActiveCount, description: "Number of Active Timers");
        }
    }
}
