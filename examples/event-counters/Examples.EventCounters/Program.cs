// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;
using OpenTelemetry.Metrics;

// Create EventSources and EventCounters
ThreadLocal<Random> random = new(() => new Random());
using EventSource eventSource = new("MyEventSource");
using EventCounter eventCounter = new("MyEventCounter", eventSource);
using PollingCounter pollingCounter = new("MyPollingCounter", eventSource, () => random.Value!.NextDouble());

// Create and Configure Meter Provider
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddEventCountersInstrumentation(options =>
    {
        options.AddEventSources(eventSource.Name);
        options.RefreshIntervalSecs = 1;
    })
    .AddConsoleExporter()
    .Build();

// Write to EventCounters
eventCounter.WriteMetric(0);
eventCounter.WriteMetric(1000);

// Wait for EventCounter data to be polled (RefreshIntervalSecs is 1 second by default)
Thread.Sleep(1200);
