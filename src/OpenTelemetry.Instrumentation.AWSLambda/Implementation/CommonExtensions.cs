// <copyright file="CommonExtensions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation;

internal static class CommonExtensions
{
    internal static void AddTagIfNotNull(this List<KeyValuePair<string, object>> tags, string tagName, object? tagValue)
    {
        if (tagValue != null)
        {
            tags.Add(new(tagName, tagValue));
        }
    }

    internal static T? GetValueByKeyIgnoringCase<T>(this IDictionary<string, T> dict, string key)
    {
        // TODO: there may be opportunities for performance improvements of this method.

        // We had to introduce case-insensitive headers search as can't fully rely on
        // AWS documentation stating that expected headers are lower-case. AWS test
        // console offers JSON example with camel case header names.
        // See X-Forwarded-Proto or X-Forwarded-Port for example.

        if (dict == null)
        {
            return default;
        }

        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return default;
    }
}
