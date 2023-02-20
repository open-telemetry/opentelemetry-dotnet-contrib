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

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Enrichment;

public static class EnrichmentExtensions
{
    public static TracerProviderBuilder AddTraceEnricher<T>(this TracerProviderBuilder builder)
        where T : TraceEnricher
    {
        Guard.ThrowIfNull(builder);

        return builder
            .TryAddEnrichment()
            .ConfigureServices(services => services.AddSingleton<TraceEnricher, T>());
    }

    public static TracerProviderBuilder AddTraceEnricher(this TracerProviderBuilder builder, TraceEnricher enricher)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(enricher);

        _ = builder.TryAddEnrichment();

        if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
        {
            deferredTracerProviderBuilder.Configure((sp, builder) =>
            {
                var proc = sp.GetRequiredService<TraceEnrichmentProcessor>();
                proc.AddEnricher(enricher);
            });
        }

        return builder;
    }

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

    private static TracerProviderBuilder TryAddEnrichment(this TracerProviderBuilder builder)
    {
        return builder
            .ConfigureServices(services =>
            {
                if (!services.Any(x => x.ServiceType == typeof(TraceEnrichmentProcessor)))
                {
                    builder.AddProcessor<TraceEnrichmentProcessor>();
                }
            });
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
