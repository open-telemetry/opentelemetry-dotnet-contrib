// <copyright file="OpenTelemetryExtensionsEventSource.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// EventSource implementation for OpenTelemetry SDK extensions implementation.
/// </summary>
[EventSource(Name = "OpenTelemetry-Extensions")]
internal sealed class OpenTelemetryExtensionsEventSource : EventSource
{
    public static OpenTelemetryExtensionsEventSource Log = new OpenTelemetryExtensionsEventSource();

    [NonEvent]
    public void LogProcessorException(string @event, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
        {
            this.LogProcessorException(@event, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Unknown error in LogProcessor event '{0}': '{1}'.", Level = EventLevel.Error)]
    public void LogProcessorException(string @event, string exception)
    {
        this.WriteEvent(1, @event, exception);
    }

    [NonEvent]
    public void LogRecordFilterException(string? categoryName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.LogRecordFilterException(categoryName, ex.ToInvariantString());
        }
    }

    [Event(2, Message = "Filter threw an exception, log record will not be attached to an activity, the log record would flow to its pipeline unaffected. CategoryName: '{0}', Exception: {1}.", Level = EventLevel.Warning)]
    public void LogRecordFilterException(string? categoryName, string exception)
    {
        this.WriteEvent(2, categoryName, exception);
    }
}
