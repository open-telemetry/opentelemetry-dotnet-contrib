// <copyright file="AspNetCoreTraceEnrichmentProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore.Internal;

#pragma warning disable CA1812 // Class is instantiated through dependency injection
internal sealed class AspNetCoreTraceEnrichmentProcessor
#pragma warning restore CA1812 // Class is instantiated through dependency injection
{
    private readonly AspNetCoreTraceEnricher[] enrichers;

    public AspNetCoreTraceEnrichmentProcessor(IEnumerable<AspNetCoreTraceEnricher> enrichers)
    {
        this.enrichers = enrichers.ToArray();
    }

    public void EnrichWithHttpRequest(Activity activity, HttpRequest request)
    {
        var propertyBag = new TraceEnrichmentBag(activity);

        foreach (var enricher in this.enrichers)
        {
            enricher.EnrichWithHttpRequest(propertyBag, request);
        }
    }

    public void EnrichWithHttpResponse(Activity activity, HttpResponse response)
    {
        var propertyBag = new TraceEnrichmentBag(activity);

        foreach (var enricher in this.enrichers)
        {
            enricher.EnrichWithHttpResponse(propertyBag, response);
        }
    }

    public void EnrichWithException(Activity activity, Exception exception)
    {
        var propertyBag = new TraceEnrichmentBag(activity);

        foreach (var enricher in this.enrichers)
        {
            enricher.EnrichWithException(propertyBag, exception);
        }
    }
}
