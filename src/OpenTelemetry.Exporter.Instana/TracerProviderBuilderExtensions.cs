// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Instana;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> for using Instana.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds Instana exporter to the TracerProvider.
    /// </summary>
    /// <param name="options">The <see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddInstanaExporter(this TracerProviderBuilder options)
        => options.AddInstanaExporter(null);

    /// <summary>
    /// Adds Instana exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">The optional callback action for configuring <see cref="InstanaExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddInstanaExporter(this TracerProviderBuilder builder, Action<InstanaExporterOptions>? configure = default)
    {
#if NET
        ArgumentNullException.ThrowIfNull(builder);
#else
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
#endif

        return builder.AddProcessor((serviceProvider) =>
        {
            var options = serviceProvider.GetService<InstanaExporterOptions>() ?? new();

            ConfigureFromEnvironment(options);

            configure?.Invoke(options);

            return options.EndpointUri is null
                ? throw new InvalidOperationException("No Instana endpoint URL provided.")
                : (BaseProcessor<System.Diagnostics.Activity>)new BatchActivityExportProcessor(new InstanaExporter(options, DefaultActivityProcessor.CreateDefault()));
        });
    }

    private static void ConfigureFromEnvironment(InstanaExporterOptions options)
    {
        if (options.EndpointUri is null &&
            Environment.GetEnvironmentVariable(InstanaExporterConstants.InstanaEndpointUrl) is { Length: > 0 } endpointUrl)
        {
            options.EndpointUri = new Uri(endpointUrl, UriKind.Absolute);
        }

        if (string.IsNullOrEmpty(options.AgentKey) &&
            Environment.GetEnvironmentVariable(InstanaExporterConstants.InstanaAgentKey) is { Length: > 0 } agentKey)
        {
            options.AgentKey = agentKey;
        }

        if (Environment.GetEnvironmentVariable(InstanaExporterConstants.InstanaTimeout) is { Length: > 0 } timeout &&
            int.TryParse(timeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMilliseconds))
        {
            options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = timeoutMilliseconds;
        }

        if (options.ProxyUri is null &&
            Environment.GetEnvironmentVariable(InstanaExporterConstants.InstanaEndpointProxy) is { Length: > 0 } proxyUrl)
        {
            options.ProxyUri = new Uri(proxyUrl, UriKind.Absolute);
        }
    }
}
