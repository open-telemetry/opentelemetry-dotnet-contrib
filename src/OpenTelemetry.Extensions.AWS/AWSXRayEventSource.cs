// <copyright file="AWSXRayEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Extensions.AWS;

[EventSource(Name = "OpenTelemetry-AWS-XRay")]
internal class AWSXRayEventSource : EventSource
{
    public static AWSXRayEventSource Log = new();

    [NonEvent]
    public void ActivityContextExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.FailedToExtractActivityContext(format, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Failed to extract activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToExtractActivityContext(string format, string exception)
    {
        this.WriteEvent(1, format, exception);
    }

    [Event(2, Message = "Failed to inject activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToInjectActivityContext(string format, string error)
    {
        this.WriteEvent(2, format, error);
    }
}
