// <copyright file="TestIncrementingEventCounter.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests;

[EventSource(Name = "OpenTelemetry.Instrumentation.EventCounters.Tests.TestIncrementingEventCounter")]
public sealed class TestIncrementingEventCounter : EventSource
{
    public const string CounterName = "IncrementingEventCounter";

    // define the singleton instance of the event source
    public static TestIncrementingEventCounter Log = new();
    private IncrementingEventCounter incrementingEventCounter;

    private TestIncrementingEventCounter()
    {
        this.incrementingEventCounter = new IncrementingEventCounter(CounterName, this);
    }

    public void SampleCounter1(float counterValue)
    {
        this.incrementingEventCounter.Increment(counterValue);
    }
}
