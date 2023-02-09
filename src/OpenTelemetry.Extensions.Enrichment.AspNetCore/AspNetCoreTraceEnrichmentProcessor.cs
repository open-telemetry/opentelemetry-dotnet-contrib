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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.ObjectPool;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore;

internal sealed class AspNetCoreTraceEnrichmentProcessor
{
    private readonly List<BaseAspNetCoreTraceEnricher> enrichers = new();

    private readonly ObjectPool<TraceEnrichmentBag> propertyBagPool =
        new DefaultObjectPool<TraceEnrichmentBag>(PooledBagPolicy<TraceEnrichmentBag>.Instance);

    public AspNetCoreTraceEnrichmentProcessor(IEnumerable<BaseAspNetCoreTraceEnricher> enrichers)
    {
        this.enrichers.AddRange(enrichers);
    }

    public void AddEnricher(BaseAspNetCoreTraceEnricher enrichers)
    {
        Debug.Assert(enrichers != null, "enricher was null");

        this.enrichers.Add(enrichers!);
    }

    public void EnrichWithHttpRequest(Activity activity, HttpRequest request)
    {
        var propertyBag = this.propertyBagPool.Get();

        try
        {
            propertyBag.Activity = activity;

            foreach (var enricher in this.enrichers)
            {
                enricher.EnrichWithHttpRequest(propertyBag, request);
            }
        }
        finally
        {
            this.propertyBagPool.Return(propertyBag);
        }
    }

    public void EnrichWithHttpResponse(Activity activity, HttpResponse response)
    {
        var propertyBag = this.propertyBagPool.Get();

        try
        {
            propertyBag.Activity = activity;

            foreach (var enricher in this.enrichers)
            {
                enricher.EnrichWithHttpResponse(propertyBag, response);
            }
        }
        finally
        {
            this.propertyBagPool.Return(propertyBag);
        }
    }

    public void EnrichWithException(Activity activity, Exception exception)
    {
        var propertyBag = this.propertyBagPool.Get();

        try
        {
            propertyBag.Activity = activity;

            foreach (var enricher in this.enrichers)
            {
                enricher.EnrichWithException(propertyBag, exception);
            }
        }
        finally
        {
            this.propertyBagPool.Return(propertyBag);
        }
    }
}
