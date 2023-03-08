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

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Extensions.Enrichment;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to register telemery enrichers.
/// </summary>
public static class OpenTelemetryEnrichmentServiceCollectionExtensions
{
    /// <summary>
    /// Adds trace enricher.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> being configured.</param>
    /// <typeparam name="T">Enricher object type.</typeparam>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="IServiceCollection"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static IServiceCollection AddTraceEnricher<T>(this IServiceCollection services)
        where T : TraceEnricher
    {
        Guard.ThrowIfNull(services);

        return services
            .TryAddEnrichment()
            .AddSingleton<TraceEnricher, T>();
    }

    /// <summary>
    /// Adds trace enricher.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> being configured.</param>
    /// <param name="enricher">The <see cref="TraceEnricher"/> object being added.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> or <paramref name="enricher"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="IServiceCollection"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static IServiceCollection AddTraceEnricher(this IServiceCollection services, TraceEnricher enricher)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(enricher);

        return services
            .TryAddEnrichment()
            .AddSingleton(enricher);
    }

    /// <summary>
    /// Adds trace enricher.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> being configured.</param>
    /// <param name="enrichmentAction">The <see cref="Action"/> delegate to enrich traces.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> or <paramref name="enrichmentAction"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="IServiceCollection"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static IServiceCollection AddTraceEnricher(this IServiceCollection services, Action<TraceEnrichmentBag> enrichmentAction)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(enrichmentAction);

        services.TryAddSingleton<TraceEnricher, EnrichmentActions>();

        return services
            .TryAddEnrichment()
            .AddSingleton(enrichmentAction);
    }

    /// <summary>
    /// Adds trace enricher.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> being configured.</param>
    /// <param name="enricherImplementationFactory">The <see cref="TraceEnricher"/> object being added using implementation factory.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> or <paramref name="enricherImplementationFactory"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="IServiceCollection"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static IServiceCollection AddTraceEnricher(this IServiceCollection services, Func<IServiceProvider, TraceEnricher> enricherImplementationFactory)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(enricherImplementationFactory);

        return services
            .TryAddEnrichment()
            .AddSingleton((serviceProvider) => enricherImplementationFactory(serviceProvider));
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
