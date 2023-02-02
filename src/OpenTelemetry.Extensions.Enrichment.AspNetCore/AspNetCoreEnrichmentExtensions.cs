// <copyright file="AspNetCoreEnrichmentExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Options;
using Microsoft.R9.Extensions.HttpClient.Tracing.Internal;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore;

public static class AspNetCoreEnrichmentExtensions
{
    public static TracerProviderBuilder AddAspNetCoreTraceEnricher<T>(this TracerProviderBuilder builder)
        where T : BaseAspNetCoreTraceEnricher
    {
        Guard.ThrowIfNull(builder);

        return builder
            .ConfigureServices(services => services
                .TryAddAspNetCoreEnrichment()
                .AddSingleton<BaseAspNetCoreTraceEnricher, T>());
    }

    public static TracerProviderBuilder AddAspNetCoreTraceEnricher(this TracerProviderBuilder builder, BaseAspNetCoreTraceEnricher enricher)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(enricher);

        return builder
            .ConfigureServices(services => services
                .TryAddAspNetCoreEnrichment())
            .ConfigureBuilder((sp, builder) =>
            {
                var proc = sp.GetRequiredService<AspNetCoreTraceEnrichmentProcessor>();
                proc.AddEnricher(enricher);
            });
    }

    public static IServiceCollection AddAspNetCoreTraceEnricher<T>(this IServiceCollection services)
        where T : BaseAspNetCoreTraceEnricher
    {
        Guard.ThrowIfNull(services);

        return services
            .TryAddAspNetCoreEnrichment()
            .AddSingleton<BaseAspNetCoreTraceEnricher, T>();
    }

    public static IServiceCollection AddAspNetCoreTraceEnricher(this IServiceCollection services, BaseAspNetCoreTraceEnricher enricher)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(enricher);

        return services
            .TryAddAspNetCoreEnrichment()
            .AddSingleton(enricher);
    }

    private static IServiceCollection TryAddAspNetCoreEnrichment(this IServiceCollection services)
    {
        services.AddSingleton<IConfigureOptions<AspNetCoreInstrumentationOptions>, ConfigureAspNetCoreInstrumentationOptions>();
        services.TryAddSingleton<AspNetCoreTraceEnrichmentProcessor>();

        return services;
    }
}
