// <copyright file="AWSSamplerEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Sampler.AWS;

[EventSource(Name = "OpenTelemetry-Sampler-AWS")]
internal class AWSSamplerEventSource : EventSource
{
    public static AWSSamplerEventSource Log = new AWSSamplerEventSource();

    [Event(1, Message = "Error response from {0} with status code {1}", Level = EventLevel.Warning)]
    public void FailedToGetSuccessResponse(string endpoint, string statusCode)
    {
        this.WriteEvent(1, endpoint, statusCode);
    }

    [Event(2, Message = "Exception from AWSXRayRemoteSampler while executing request {0})}", Level = EventLevel.Warning)]
    public void ExceptionFromSampler(string message)
    {
        this.WriteEvent(2, message);
    }

    [Event(3, Message = "Failed to deserialize to object in format: '{0}', error: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToDeserializeResponse(string format, string error)
    {
        this.WriteEvent(3, format, error);
    }

    [Event(4, Message = "Using fallback sampler. Either rules cache has expired or no rules matched the request.", Level = EventLevel.Informational)]
    public void InfoUsingFallbackSampler()
    {
        this.WriteEvent(4);
    }
}
