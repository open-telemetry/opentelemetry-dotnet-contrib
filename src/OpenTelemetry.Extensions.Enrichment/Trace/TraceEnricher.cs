// <copyright file="TraceEnricher.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Extensions.Enrichment;

/// <summary>
/// Interface for implementing enrichers for traces.
/// </summary>
public abstract class TraceEnricher
{
    protected TraceEnricher()
    {
    }

    /// <summary>
    /// Enrich trace with desired tags.
    /// </summary>
    /// <param name="enrichmentBag"><see cref="TraceEnrichmentBag"/> object to be used to add the required tags to enrich the traces.</param>
    public abstract void Enrich(in TraceEnrichmentBag enrichmentBag);

    /// <summary>
    /// Enrich trace with desired tags at the Start event of the <see cref="Activity"/>.
    /// </summary>
    /// <param name="enrichmentBag"><see cref="TraceEnrichmentBag"/> object to be used to add the required tags to enrich the traces.</param>
    public virtual void EnrichOnActivityStart(in TraceEnrichmentBag enrichmentBag)
    {
        // default implementation: noop
    }
}
