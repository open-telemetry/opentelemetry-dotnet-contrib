// <copyright file="SpecificTagFilter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Filters;

/// <summary>
/// A filter used to keep these data with one of the specified tags.
/// </summary>
public class SpecificTagFilter : BaseFilter<Activity>
{
    private const string Description = "A filter used to keep these data with one of the specified tags.";
    private readonly IReadOnlyDictionary<string, string> filterMappings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpecificTagFilter"/> class.
    /// </summary>
    /// <param name="filterMappings">key value pairs for filtering.</param>
    public SpecificTagFilter(Dictionary<string, string> filterMappings)
    {
        Guard.ThrowIfNull(filterMappings, nameof(filterMappings));
        this.filterMappings = filterMappings;
    }

    /// <inheritdoc/>
    public override string GetDescription()
    {
        return Description;
    }

    /// <summary>
    /// check if the tags contains one of the filter key value pairs, else return true and will be dropped.
    /// </summary>
    /// <param name="t">completed activity.</param>
    /// <returns>if true returned, data will be dropped. Else will be kept.</returns>
    public override bool ShouldFilter(Activity t)
    {
        if (t == null)
        {
            return true;
        }

        foreach (var kvp in t.Tags)
        {
            if (this.filterMappings.ContainsKey(kvp.Key))
            {
                return !string.Equals(this.filterMappings[kvp.Key] ?? string.Empty, kvp.Value ?? string.Empty, System.StringComparison.Ordinal);
            }
        }

        return true;
    }
}
