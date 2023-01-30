// <copyright file="EnrichmentExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Enrichment;

public static class EnrichmentExtensions
{
    public static TracerProviderBuilder AddTraceEnricher<T>(this TracerProviderBuilder builder)
        where T : BaseTraceEnricher
    {
        Guard.ThrowIfNull(builder);

        return builder
            .ConfigureServices(services => services.AddSingleton<BaseTraceEnricher, T>())
            .TryAddEnrichment();
    }

    public static TracerProviderBuilder AddTraceEnricher<T>(this TracerProviderBuilder builder, BaseTraceEnricher enricher)
        where T : BaseTraceEnricher
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(enricher);

        return builder
            .ConfigureServices(services => services.AddSingleton(enricher))
            .TryAddEnrichment();
    }

    private static TracerProviderBuilder TryAddEnrichment(this TracerProviderBuilder builder)
    {
        return builder
            .ConfigureServices(services => services.TryAddSingleton<TraceEnrichmentProcessor>())
            .AddProcessor<TraceEnrichmentProcessor>();
    }
}
