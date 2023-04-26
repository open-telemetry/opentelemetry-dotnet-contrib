// <copyright file="ExporterStackdriverEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Stackdriver.Implementation;

[EventSource(Name = "OpenTelemetry-Exporter-Stackdriver")]
internal class ExporterStackdriverEventSource : EventSource
{
    public static readonly ExporterStackdriverEventSource Log = new();

    [NonEvent]
    public void ExportMethodException(Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.ExportMethodException(ex.ToInvariantString());
        }
    }

    [Event(3, Message = "Stackdriver exporter encountered an error while exporting. Exception: {0}", Level = EventLevel.Error)]
    public void ExportMethodException(string ex)
    {
        this.WriteEvent(1, ex);
    }
}
