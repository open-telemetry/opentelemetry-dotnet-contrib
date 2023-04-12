// <copyright file="EnrichmentActions.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Extensions.Enrichment;

#pragma warning disable CA1812 // Class is instantiated through dependency injection
internal sealed class EnrichmentActions : TraceEnricher
#pragma warning restore CA1812 // Class is instantiated through dependency injection
{
    private readonly Action<TraceEnrichmentBag>[] actions;

    public EnrichmentActions(IEnumerable<Action<TraceEnrichmentBag>> actions)
    {
        this.actions = actions.ToArray();
    }

    public override void Enrich(in TraceEnrichmentBag enrichmentBag)
    {
        for (int i = 0; i < this.actions.Length; i++)
        {
            this.actions[i].Invoke(enrichmentBag);
        }
    }
}
