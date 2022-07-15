// <copyright file="TestPollingEventCounter.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests
{
    [EventSource(Name = "OpenTelemetry.Instrumentation.EventCounters.Tests.TestPollingEventCounter")]
    public sealed class TestPollingEventCounter : EventSource
    {
        public const string CounterName = "TestPollingCounter";

        private PollingCounter pollingCounter;

        private TestPollingEventCounter(Func<double> provider)
        {
            this.pollingCounter = new PollingCounter(CounterName, this, provider);
        }

        // define the singleton instance of the event source
        public static TestPollingEventCounter CreateSingleton(Func<double> provider) => new(provider);
    }
}
