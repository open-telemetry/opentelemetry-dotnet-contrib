// <copyright file="OpenTelemetryEnrichmentServiceCollectionExtensions.cs" company="OpenTelemetry Authors">
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

using System.Linq;
using OpenTelemetry.Extensions.Enrichment;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

public static class OpenTelemetryEnrichmentServiceCollectionExtensions
{
    public static IServiceCollection AddTraceEnricher<T>(this IServiceCollection services)
        where T : TraceEnricher
    {
        Guard.ThrowIfNull(services);

        return services
            .TryAddEnrichment()
            .AddSingleton<TraceEnricher, T>();
    }

    public static IServiceCollection AddTraceEnricher(this IServiceCollection services, TraceEnricher enricher)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(enricher);

        return services
            .TryAddEnrichment()
            .AddSingleton(enricher);
    }

    private static IServiceCollection TryAddEnrichment(this IServiceCollection services)
    {
        if (!services.Any(x => x.ServiceType == typeof(TraceEnrichmentProcessor)))
        {
            services
                .AddSingleton<TraceEnrichmentProcessor>()
                .ConfigureOpenTelemetryTracerProvider((sp, builder) =>
                {
                    var proc = sp.GetRequiredService<TraceEnrichmentProcessor>();
                    builder.AddProcessor(proc);
                });
        }

        return services;
    }
}
