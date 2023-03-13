// <copyright file="OpenTelemetryEnrichmentProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Enrichment;

/// <summary>
/// Extension methods to register telemery enrichers.
/// </summary>
public static class OpenTelemetryEnrichmentProviderBuilderExtensions
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
    public static TracerProviderBuilder AddTraceEnricher<T>(this TracerProviderBuilder builder)
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
    /// <param name="enrichmentAction">The <see cref="Action"/> delegate to enrich traces.</param>
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
