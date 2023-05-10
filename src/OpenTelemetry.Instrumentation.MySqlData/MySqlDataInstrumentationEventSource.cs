// <copyright file="MySqlDataInstrumentationEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.MySqlData;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Instrumentation-MySqlData")]
internal class MySqlDataInstrumentationEventSource : EventSource
{
    public static readonly MySqlDataInstrumentationEventSource Log = new MySqlDataInstrumentationEventSource();

    [Event(1, Message = "Unknown MySqlTraceEventType: {0}, Message {1}", Level = EventLevel.Error)]
    public void UnknownMySqlTraceEventType(int mysqlEventId, string message)
    {
        this.WriteEvent(1, mysqlEventId, message);
    }

    [Event(2, Message = "Error while processing trace event, MySqlTraceEventType: {0}, Message {1}, Exception: {2}", Level = EventLevel.Error)]
    public void ErrorTraceEvent(int mysqlEventId, string message, string exception)
    {
        this.WriteEvent(1, mysqlEventId, message, exception);
    }

    [Event(3, Message = "Error while initializing MySqlDataInstrumentation, Message {0}, Exception: {1}", Level = EventLevel.Warning)]
    public void ErrorInitialize(string message, string exception)
    {
        this.WriteEvent(1, message, exception);
    }
}
