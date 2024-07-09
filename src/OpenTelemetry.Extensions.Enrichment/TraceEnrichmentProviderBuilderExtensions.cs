// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Enrichment;

/// <summary>
/// Extension methods to register telemetry enrichers.
/// </summary>
public static class TraceEnrichmentProviderBuilderExtensions
{
    /// <summary>
    /// Adds trace enricher.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <typeparam name="T">Enricher object type.</typeparam>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
#if NET
    public static TracerProviderBuilder AddTraceEnricher<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this TracerProviderBuilder builder)
#else
    public static TracerProviderBuilder AddTraceEnricher<T>(this TracerProviderBuilder builder)
#endif
        where T : TraceEnricher
    {
        Guard.ThrowIfNull(builder);

        return builder
            .ConfigureServices(services => services.AddTraceEnricher<T>());
    }

    /// <summary>
    /// Adds trace enricher.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="enricher">The <see cref="TraceEnricher"/> object being added.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> or <paramref name="enricher"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static TracerProviderBuilder AddTraceEnricher(this TracerProviderBuilder builder, TraceEnricher enricher)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(enricher);

        return builder
            .ConfigureServices(services => services.AddTraceEnricher(enricher));
    }

    /// <summary>
    /// Adds trace enricher.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="enrichmentAction">The <see cref="TraceEnrichmentBag"/> delegate to enrich traces.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> or <paramref name="enrichmentAction"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static TracerProviderBuilder AddTraceEnricher(this TracerProviderBuilder builder, Action<TraceEnrichmentBag> enrichmentAction)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(enrichmentAction);

        return builder
            .ConfigureServices(services => services.AddTraceEnricher(enrichmentAction));
    }

    /// <summary>
    /// Adds trace enricher.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="enricherImplementationFactory">The <see cref="TraceEnricher"/> object being added using implementation factory.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> or <paramref name="enricherImplementationFactory"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static TracerProviderBuilder AddTraceEnricher(this TracerProviderBuilder builder, Func<IServiceProvider, TraceEnricher> enricherImplementationFactory)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(enricherImplementationFactory);

        return builder
            .ConfigureServices(services => services
                .AddTraceEnricher(enricherImplementationFactory));
    }
}
