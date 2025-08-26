// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.OpenSearchClient;
using OpenTelemetry.Instrumentation.OpenSearchClient.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables OpenSearch client Instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOpenSearchClientInstrumentation(
        this TracerProviderBuilder builder) =>
        AddOpenSearchClientInstrumentation(builder, name: null, configure: null);

    /// <summary>
    /// Enables OpenSearch client Instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">OpenSearch client configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOpenSearchClientInstrumentation(
        this TracerProviderBuilder builder,
        Action<OpenSearchClientInstrumentationOptions>? configure) =>
        AddOpenSearchClientInstrumentation(builder, name: null, configure);

    /// <summary>
    /// Enables OpenSearch client Instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">OpenSearch client configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOpenSearchClientInstrumentation(
        this TracerProviderBuilder builder,
        string? name,
        Action<OpenSearchClientInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configure));
        }

        builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<OpenSearchClientInstrumentationOptions>>().Get(name);
            return new OpenSearchClientInstrumentation(options);
        });

        builder.AddSource(OpenSearchRequestPipelineDiagnosticListener.ActivitySourceName);
        builder.AddLegacySource("CallOpenSearch");

        return builder;
    }
}
