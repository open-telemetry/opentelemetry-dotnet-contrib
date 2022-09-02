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
using System.Linq;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation
{
    internal static class CommonExtensions
    {
        internal static bool TryGetValueIgnoringCase<T>(this IDictionary<string, T> dict, string key, out T value)
        {
            value = default;
            var targetKey = dict.Keys
                .Where(s => string.Equals(s, key, StringComparison.CurrentCultureIgnoreCase))
                .FirstOrDefault();

            if (targetKey != default)
            {
                value = dict[targetKey];
                return true;
            }

            return false;
        }

        internal static void AddStringTagIfNotNull(this List<KeyValuePair<string, object>> tags, string tagName, string tagValue)
        {
            if (tagValue != null)
            {
                tags.Add(new(tagName, tagValue));
            }
        }
    }
}
