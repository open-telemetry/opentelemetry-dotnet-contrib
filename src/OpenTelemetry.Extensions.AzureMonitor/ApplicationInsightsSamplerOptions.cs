// <copyright file="ApplicationInsightsSamplerOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Extensions.AzureMonitor;

/// <summary>
/// Options for configuring an <see cref="ApplicationInsightsSampler"/> to customize
/// its sampling behavior.
/// </summary>
public class ApplicationInsightsSamplerOptions
{
    /// <summary>
    /// Gets or sets the ratio of telemetry items to be sampled. The value must be between 0.0F and 1.0F, inclusive.
    /// For example, specifying 0.4 means that 40% of traces are sampled and 60% are dropped.
    /// </summary>
    public float SamplingRatio { get; set; }
}
