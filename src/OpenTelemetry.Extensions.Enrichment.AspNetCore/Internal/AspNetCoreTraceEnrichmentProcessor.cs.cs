// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore;

#pragma warning disable CA1812 // Class is instantiated through dependency injection
internal sealed class AspNetCoreTraceEnrichmentProcessor
#pragma warning restore CA1815 // Class is instantiated through dependency injection
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
            enricher.EnrichWithHttpRequest(in propertyBag, request);
        }
    }

    public void EnrichWithHttpResponse(Activity activity, HttpResponse response)
    {
        var propertyBag = new TraceEnrichmentBag(activity);

        foreach (var enricher in this.enrichers)
        {
            enricher.EnrichWithHttpResponse(in propertyBag, response);
        }
    }

    public void EnrichWithException(Activity activity, Exception exception)
    {
        var propertyBag = new TraceEnrichmentBag(activity);

        foreach (var enricher in this.enrichers)
        {
            enricher.EnrichWithException(in propertyBag, exception);
        }
    }
}
