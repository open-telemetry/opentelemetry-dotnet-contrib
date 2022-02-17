// <copyright file="ExceptionsInstrumentation.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    internal class ExceptionsInstrumentation : IRuntimeInstrumentation, IDisposable
    {
        private const string CounterName = "exception-count";

        private readonly Meter meter;
        private readonly IEventCounterStore eventCounterStore;
        private readonly ObservableGauge<long> exceptionCounter;

        public ExceptionsInstrumentation(Meter meter, IEventCounterStore eventCounterStore)
        {
            this.meter = meter;
            this.eventCounterStore = eventCounterStore;
            this.eventCounterStore.Subscribe(CounterName, EventCounterType.Sum);

            this.exceptionCounter = meter.CreateObservableGauge("exception-count", () => this.eventCounterStore.ReadLong(CounterName), description: "Exception Count");
        }

        public void Dispose()
        {
            this.eventCounterStore?.Unsubscribe(CounterName);
        }
    }
}
