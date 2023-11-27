// <copyright file="SamplerFilter.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Filters;

/// <summary>
/// A Filter using sampler logic to deicde whether to filter.
/// </summary>
public class SamplerFilter : BaseFilter<Activity>
{
    private const string Description = "A Filter using sampler logic to decide whether to filter.";
    private readonly Sampler sampler;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplerFilter"/> class.
    /// </summary>
    /// <param name="sampler">predefined sampler.</param>
    public SamplerFilter(Sampler sampler)
    {
        this.sampler = sampler;
    }

    /// <inheritdoc/>
    public override string GetDescription()
    {
        return Description;
    }

    /// <summary>
    /// decide whether to filter data by the sampling result.
    /// </summary>
    /// <param name="t">completed activity.</param>
    /// <returns>filter result.</returns>
    public override bool ShouldFilter(Activity t)
    {
        if (t == null)
        {
            return true;
        }

        var samplingParameters = new SamplingParameters(
            default,
            t.TraceId,
            t.DisplayName,
            t.Kind,
            t.TagObjects,
            t.Links);

        return !this.sampler.ShouldSample(samplingParameters).Decision.Equals(SamplingDecision.RecordAndSample);
    }
}
