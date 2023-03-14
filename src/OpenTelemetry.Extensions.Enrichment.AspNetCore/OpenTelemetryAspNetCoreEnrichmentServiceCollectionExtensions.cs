// <copyright file="OpenTelemetryAspNetCoreEnrichmentServiceCollectionExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Extensions.Enrichment.AspNetCore;
using OpenTelemetry.Extensions.Enrichment.AspNetCore.Internal;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to register ASP.NET Core telemery enrichers.
/// </summary>
public static class OpenTelemetryAspNetCoreEnrichmentServiceCollectionExtensions
{
    /// <summary>
    /// Adds trace enricher for incoming HTTP requests.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> being configured.</param>
    /// <typeparam name="T">Enricher object type.</typeparam>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="services"/> is <see langword="null" />.</exception>
    /// <returns>The instance of <see cref="IServiceCollection"/> to chain the calls.</returns>
    /// <remarks>
    /// Add this enricher *before* exporter related Activity processors.
    /// </remarks>
    public static IServiceCollection AddAspNetCoreTraceEnricher<T>(this IServiceCollection services)
        where T : AspNetCoreTraceEnricher
    {
        Guard.ThrowIfNull(services);

        return services
            .TryAddAspNetCoreEnrichment()
            .AddSingleton<AspNetCoreTraceEnricher, T>();
    }

    /// <summary>
    /// Adds trace enricher for incoming HTTP requests.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> being configured.</param>
    /// <param name="enricher">The <see cref="AspNetCoreTraceEnricher"/> object being added.</param>
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
            .TryAddAspNetCoreEnrichment()
            .AddSingleton(enricher);
    }

    /// <summary>
    /// Adds trace enricher for incoming HTTP requests.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> being configured.</param>
    /// <param name="enricherImplementationFactory">The <see cref="AspNetCoreTraceEnricher"/> object being added using implementation factory.</param>
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
            .TryAddAspNetCoreEnrichment()
            .AddSingleton((serviceProvider) => enricherImplementationFactory(serviceProvider));
    }

    private static IServiceCollection TryAddAspNetCoreEnrichment(this IServiceCollection services)
    {
        services.AddSingleton<IConfigureOptions<AspNetCoreInstrumentationOptions>, ConfigureAspNetCoreInstrumentationOptions>();
        services.TryAddSingleton<AspNetCoreTraceEnrichmentProcessor>();

        return services;
    }
}
