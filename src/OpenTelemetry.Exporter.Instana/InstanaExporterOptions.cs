// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

using System.Diagnostics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Instana;

/// <summary>
/// A class representing the options for configuring the Instana exporter.
/// </summary>
public class InstanaExporterOptions
{
    /// <summary>
    /// Gets or sets the key used to authenticate with the Instana agent.
    /// </summary>
    public string AgentKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the <see cref="BatchExportProcessorOptions{Activity}"/> options to use.
    /// </summary>
    public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; } = new() { ExporterTimeoutMilliseconds = 20_000 };

    /// <summary>
    /// Gets or sets the URI of the Instana endpoint.
    /// </summary>
    public Uri EndpointUri { get; set; } = default!;

    /// <summary>
    /// Gets or sets an optional delegate to a method to configure
    /// the <see cref="HttpClient"/> to use to send telemetry to the Instana endpoint.
    /// </summary>
    public Func<HttpClient>? HttpClientFactory { get; set; }

    /// <summary>
    /// Gets or sets the optional proxy URI to use when sending data to the Instana endpoint.
    /// </summary>
    public Uri? ProxyUri { get; set; }

    /// <summary>
    /// Gets or sets a delegate to a method that returns the current UTC time.
    /// </summary>
    public Func<DateTimeOffset> UtcNow { get; set; } =
#if NET
        TimeProvider.System.GetUtcNow;
#else
        static () => DateTimeOffset.UtcNow;
#endif

    internal Func<BaseExporter<Activity>, Resource> GetParentProviderResource { get; set; } = static (exporter) => exporter.ParentProvider.GetResource();
}
