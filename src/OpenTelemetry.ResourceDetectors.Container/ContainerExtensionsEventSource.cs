// <copyright file="ContainerExtensionsEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.ResourceDetectors.Container;

[EventSource(Name = "OpenTelemetry-ResourceDetectors-Container")]
internal class ContainerExtensionsEventSource : EventSource
{
    public static ContainerExtensionsEventSource Log = new();

    [NonEvent]
    public void ExtractResourceAttributesException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
        {
            this.FailedToExtractResourceAttributes(format, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Error)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(1, format, exception);
    }
}
