// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Extensions.Enrichment;
using OpenTelemetry.Extensions.Enrichment.AspNetCore;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to register telemetry enrichers.
/// </summary>
public static class AspNetCoreTraceEnrichmentServiceCollectionExtensions
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
#if NET
    public static IServiceCollection AddAspNetCoreTraceEnricher<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services)
#else
    public static IServiceCollection AddAspNetCoreTraceEnricher<T>(this IServiceCollection services)
#endif
        where T : AspNetCoreTraceEnricher
    {
        Guard.ThrowIfNull(services);

        return services
            .TryAddEnrichment()
            .AddSingleton<AspNetCoreTraceEnricher, T>();
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
    public static IServiceCollection AddAspNetCoreTraceEnricher(this IServiceCollection services, AspNetCoreTraceEnricher enricher)
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
    /// <param name="enrichmentAction">The <see cref="TraceEnrichmentBag"/> delegate to enrich traces.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> or <paramref name="enrichmentAction"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="IServiceCollection"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static IServiceCollection AddAspNetCoreTraceEnricher(this IServiceCollection services, Action<TraceEnrichmentBag> enrichmentAction)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(enrichmentAction);

        services.TryAddSingleton<AspNetCoreTraceEnricher, AspNetCoreTraceEnrichmentActions>();

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
    public static IServiceCollection AddAspNetCoreTraceEnricher(this IServiceCollection services, Func<IServiceProvider, AspNetCoreTraceEnricher> enricherImplementationFactory)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(enricherImplementationFactory);

        return services
            .TryAddEnrichment()
            .AddSingleton((serviceProvider) => enricherImplementationFactory(serviceProvider));
    }

    private static IServiceCollection TryAddEnrichment(this IServiceCollection services)
    {
        if (!services.Any(x => x.ServiceType == typeof(AspNetCoreTraceEnrichmentProcessor)))
        {
            services
                .AddSingleton<AspNetCoreTraceEnrichmentProcessor>()
                .ConfigureOpenTelemetryTracerProvider((sp, builder) =>
                {
                    var proc = sp.GetRequiredService<AspNetCoreTraceEnrichmentProcessor>();
                    builder.AddProcessor(proc);
                });
        }

        return services;
    }
}
