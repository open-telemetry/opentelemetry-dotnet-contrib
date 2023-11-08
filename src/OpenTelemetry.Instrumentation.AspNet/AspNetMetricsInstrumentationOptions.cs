// <copyright file="AspNetMetricsInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Web;

namespace OpenTelemetry.Instrumentation.AspNet;

/// <summary>
/// Options for metric instrumentation.
/// </summary>
public sealed class AspNetMetricsInstrumentationOptions
{
    /// <summary>
    /// Delegate for enrichment of recorded metric with additional tags.
    /// </summary>
    /// <param name="context"><see cref="HttpContext"/>: the HttpContext object. Both Request and Response are available.</param>
    /// <param name="tags"><see cref="TagList"/>: List of current tags. You can add additional tags to this list. </param>
    public delegate void EnrichFunc(HttpContext context, ref TagList tags);

    /// <summary>
    /// Gets or sets an function to enrich a recorded metric with additional custom tags.
    /// </summary>
    public EnrichFunc? Enrich { get; set; }
}
