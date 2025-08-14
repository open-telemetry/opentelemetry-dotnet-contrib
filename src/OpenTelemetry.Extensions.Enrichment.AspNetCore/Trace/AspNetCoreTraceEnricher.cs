// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore;

public abstract class AspNetCoreTraceEnricher
{
    public virtual void EnrichWithHttpRequest(in TraceEnrichmentBag enrichmentBag, HttpRequest request)
    {
    }

    public virtual void EnrichWithHttpResponse(in TraceEnrichmentBag enrichmentBag, HttpResponse response)
    {
    }

    public virtual void EnrichWithException(in TraceEnrichmentBag enrichmentBag, Exception exception)
    {
    }
}
