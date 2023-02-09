// <copyright file="TraceEnrichmentProcessor.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.ObjectPool;

namespace OpenTelemetry.Extensions.Enrichment;

internal sealed class TraceEnrichmentProcessor : BaseProcessor<Activity>
{
    private readonly List<BaseTraceEnricher> traceEnrichers = new();
    private readonly ObjectPool<TraceEnrichmentBag> propertyBagPool =
        new DefaultObjectPool<TraceEnrichmentBag>(PooledBagPolicy<TraceEnrichmentBag>.Instance);

    public TraceEnrichmentProcessor(IEnumerable<BaseTraceEnricher> traceEnrichers)
    {
        this.traceEnrichers.AddRange(traceEnrichers);
    }

    public void AddEnricher(BaseTraceEnricher traceEnricher)
    {
        Debug.Assert(traceEnricher != null, "trace enricher was null");

        this.traceEnrichers.Add(traceEnricher!);
    }

    public override void OnEnd(Activity activity)
    {
        var propertyBag = this.propertyBagPool.Get();

        try
        {
            propertyBag.Activity = activity;

            foreach (var enricher in this.traceEnrichers)
            {
                enricher.Enrich(propertyBag);
            }
        }
        finally
        {
            this.propertyBagPool.Return(propertyBag);
        }
    }
}
