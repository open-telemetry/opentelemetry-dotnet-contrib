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
EventSource eventSource = new("MyEventSource");
EventCounter eventCounter = new("MyEventCounter", eventSource);
PollingCounter pollingCounter = new("MyPollingCounter", eventSource, () => new Random().NextDouble());

// Create and Configure Meter Provider
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddEventCountersInstrumentation(options =>
    {
        options.RefreshIntervalSecs = 1;
        options.AddEventSources(eventSource.Name);
    })
    .AddConsoleExporter()
    .Build();

eventCounter.WriteMetric(0);
eventCounter.WriteMetric(1000);

Thread.Sleep(1500);
