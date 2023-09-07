// <copyright file="Utils.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Sampler.AWS.Tests;

internal static class Utils
{
    internal static SamplingParameters CreateSamplingParameters()
    {
        return CreateSamplingParametersWithTags(new Dictionary<string, string>());
    }

    internal static SamplingParameters CreateSamplingParametersWithTags(Dictionary<string, string> tags)
    {
        ActivityTraceId traceId = ActivityTraceId.CreateRandom();
        ActivitySpanId parentSpanId = ActivitySpanId.CreateRandom();
        ActivityTraceFlags traceFlags = ActivityTraceFlags.None;

        var parentContext = new ActivityContext(traceId, parentSpanId, traceFlags);

        var tagList = new List<KeyValuePair<string, object?>>();

        foreach (var tag in tags)
        {
            tagList.Add(new KeyValuePair<string, object?>(tag.Key, tag.Value));
        }

        return new SamplingParameters(
            parentContext,
            traceId,
            "myActivityName",
            ActivityKind.Server,
            tagList,
            null);
    }

    internal static Resource CreateResource(string serviceName, string cloudPlatform)
    {
        var resourceAttributes = new List<KeyValuePair<string, object>>
        {
            new("service.name", serviceName),
            new("cloud.platform", cloudPlatform),
        };

        return ResourceBuilder.CreateEmpty().AddAttributes(resourceAttributes).Build();
    }
}
