// <copyright file="AspNetCoreTraceEnricher.cs" company="OpenTelemetry Authors">
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
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore;

/// <summary>
/// Represents ASP.NET Core trace enricher class.
/// </summary>
public abstract class AspNetCoreTraceEnricher : BaseAspNetCoreEnricher<TraceEnrichmentBag>
{
    /// <inheritdoc/>
    public override void EnrichWithHttpRequest(ref TraceEnrichmentBag enrichmentBag, HttpRequest request)
    {
    }

    /// <inheritdoc/>
    public override void EnrichWithHttpResponse(ref TraceEnrichmentBag enrichmentBag, HttpResponse response)
    {
    }

    /// <summary>
    /// Enrich trace with desired tags at exception time.
    /// </summary>
    /// <param name="enrichmentBag">Bag used to store enrichment tags.</param>
    /// <param name="exception"><see cref="Exception"/> object representing the exception occurred.</param>
    public override void EnrichWithException(ref TraceEnrichmentBag enrichmentBag, Exception exception)
    {
    }
}
