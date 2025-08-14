// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore;

#pragma warning disable CA1812 // Class is instantiated through dependency injection
internal sealed class AspNetCoreTraceEnrichmentActions : AspNetCoreTraceEnricher
#pragma warning restore CA1812 // Class is instantiated through dependency injection
{
    private readonly Action<TraceEnrichmentBag>[] actions;

    public AspNetCoreTraceEnrichmentActions(IEnumerable<Action<TraceEnrichmentBag>> actions)
    {
        this.actions = actions.ToArray();
    }

    public override void EnrichWithHttpRequest(in TraceEnrichmentBag bag, HttpRequest request)
    {
        for (var i = 0; i < this.actions.Length; i++)
        {
            this.actions[i].Invoke(bag);
        }
    }

    public override void EnrichWithHttpResponse(in TraceEnrichmentBag bag, HttpResponse response)
    {
        for (var i = 0; i < this.actions.Length; i++)
        {
            this.actions[i].Invoke(bag);
        }
    }

    public override void EnrichWithException(in TraceEnrichmentBag bag, Exception exception)
    {
        for (var i = 0; i < this.actions.Length; i++)
        {
            this.actions[i].Invoke(bag);
        }
    }
}
