// <copyright file="AWSResourcesEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.ResourceDetectors.AWS;

[EventSource(Name = "OpenTelemetry-ResourceDetectors-AWS")]
internal class AWSResourcesEventSource : EventSource
{
    public static AWSResourcesEventSource Log = new();

    [NonEvent]
    public void ResourceAttributesExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.FailedToExtractResourceAttributes(format, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Warning)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(3, format, exception);
    }

    [Event(2, Message = "Failed to validate certificate in format: '{0}', error: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToValidateCertificate(string format, string error)
    {
        this.WriteEvent(4, format, error);
    }
}
