// <copyright file="TestEventCounter.cs" company="OpenTelemetry Authors">
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

[EventSource(Name = "OpenTelemetry.Instrumentation.EventCounters.Tests.TestEventCounter")]
public sealed class TestEventCounter : EventSource
{
    // define the singleton instance of the event source
    public static TestEventCounter Log = new TestEventCounter();
    private EventCounter testCounter1;
    private EventCounter testCounter2;

    private TestEventCounter()
    {
        this.testCounter1 = new EventCounter("mycountername1", this);
        this.testCounter2 = new EventCounter("mycountername2", this);
    }

    public void SampleCounter1(float counterValue)
    {
        this.testCounter1.WriteMetric(counterValue);
    }

    public void SampleCounter2(float counterValue)
    {
        this.testCounter2.WriteMetric(counterValue);
    }
}
