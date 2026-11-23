// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Azure.Core;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Options for the Azure Data Explorer exporter.
/// </summary>
public class AzureDataExplorerExporterOptions
{
    internal const string DefaultDatabaseName = "oteldb";
    internal const string DefaultMetricsTableName = "OTELMetrics";
    internal const string DefaultLogsTableName = "OTELLogs";
    internal const string DefaultTracesTableName = "OTELTraces";

    private TimeSpan exportTimeout = TimeSpan.FromSeconds(30);
    private Func<HttpClient> httpClientFactory = DefaultHttpClientFactory;
    private TokenCredential? defaultAzureCredential;

    /// <summary>
    /// Gets or sets the Azure Data Explorer cluster URI, for example
    /// <c>https://mycluster.westus.kusto.windows.net</c>. Required.
    /// </summary>
    /// <remarks>
    /// The engine URI should be used for both ingestion types. When using
    /// queued ingestion the data management (<c>ingest-</c>) endpoint is
    /// derived automatically.
    /// </remarks>
    public Uri? ClusterUri { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra application (client) ID used for
    /// application key authentication. Must be set together with
    /// <see cref="ApplicationKey"/> and <see cref="TenantId"/>.
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra application key (client secret) used
    /// for application key authentication.
    /// </summary>
    public string? ApplicationKey { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra tenant ID used for application key
    /// authentication.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the managed identity to authenticate with. Use
    /// <c>system</c> for a system-assigned managed identity or the client ID
    /// (a GUID) of a user-assigned managed identity.
    /// </summary>
    public string? ManagedIdentityId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to authenticate using
    /// <see cref="Azure.Identity.DefaultAzureCredential"/>. Default value: <see langword="false"/>.
    /// </summary>
    public bool UseAzureAuth { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="Azure.Core.TokenCredential"/> used to
    /// authenticate. This is an alternative to the other authentication
    /// options for applications that already construct Azure credentials.
    /// </summary>
    public TokenCredential? TokenCredential { get; set; }

    /// <summary>
    /// Gets or sets the database name. Default value: <c>oteldb</c>.
    /// </summary>
    public string DatabaseName { get; set; } = DefaultDatabaseName;

    /// <summary>
    /// Gets or sets the table metrics are ingested into. Default value: <c>OTELMetrics</c>.
    /// </summary>
    public string MetricsTableName { get; set; } = DefaultMetricsTableName;

    /// <summary>
    /// Gets or sets the table logs are ingested into. Default value: <c>OTELLogs</c>.
    /// </summary>
    public string LogsTableName { get; set; } = DefaultLogsTableName;

    /// <summary>
    /// Gets or sets the table traces are ingested into. Default value: <c>OTELTraces</c>.
    /// </summary>
    public string TracesTableName { get; set; } = DefaultTracesTableName;

    /// <summary>
    /// Gets or sets the name of an optional pre-created JSON ingestion mapping
    /// on the metrics table.
    /// </summary>
    public string? MetricsTableJsonMapping { get; set; }

    /// <summary>
    /// Gets or sets the name of an optional pre-created JSON ingestion mapping
    /// on the logs table.
    /// </summary>
    public string? LogsTableJsonMapping { get; set; }

    /// <summary>
    /// Gets or sets the name of an optional pre-created JSON ingestion mapping
    /// on the traces table.
    /// </summary>
    public string? TracesTableJsonMapping { get; set; }

    /// <summary>
    /// Gets or sets the ingestion type. Default value: <see cref="AzureDataExplorerIngestionType.Queued"/>.
    /// </summary>
    public AzureDataExplorerIngestionType IngestionType { get; set; } = AzureDataExplorerIngestionType.Queued;

    /// <summary>
    /// Gets or sets the maximum time allowed for a single export, including
    /// retries. Default value: 30 seconds.
    /// </summary>
    public TimeSpan ExportTimeout
    {
        get => this.exportTimeout;
        set
        {
            if (value <= TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "ExportTimeout must be positive or Timeout.InfiniteTimeSpan.");
            }

            this.exportTimeout = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether failed exports caused by
    /// transient (non-permanent) errors are retried with exponential backoff
    /// within <see cref="ExportTimeout"/>. Default value: <see langword="true"/>.
    /// </summary>
    public bool RetryOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the initial delay before retrying a failed export.
    /// Default value: 5 seconds.
    /// </summary>
    public TimeSpan RetryInitialInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum delay between export retries. Default value: 30 seconds.
    /// </summary>
    public TimeSpan RetryMaxInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the factory function called to create the <see
    /// cref="HttpClient"/> instance used to communicate with Azure Data
    /// Explorer, Azure Storage and the identity endpoints.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>The factory is called once for each exporter instance (one per
    /// signal).</item>
    /// <item>The exporter does not dispose the returned <see
    /// cref="HttpClient"/>; its lifetime is owned by the caller.</item>
    /// <item>The default factory returns a shared <see cref="HttpClient"/>
    /// that recycles pooled connections periodically so that DNS changes are
    /// picked up by long-running applications.</item>
    /// <item>Do not configure the returned client with a <see
    /// cref="HttpClient.BaseAddress"/>; requests are sent to absolute URIs.
    /// <see cref="ExportTimeout"/> applies independently of <see
    /// cref="HttpClient.Timeout"/>.</item>
    /// </list>
    /// </remarks>
    public Func<HttpClient> HttpClientFactory
    {
        get => this.httpClientFactory;
        set
        {
            Guard.ThrowIfNull(value);
            this.httpClientFactory = value;
        }
    }

    /// <summary>
    /// Gets or sets a factory (table name, mapping reference) used to create
    /// ingestors. Intended for testing.
    /// </summary>
    internal Func<string, string?, IAzureDataExplorerIngestor>? IngestorFactory { get; set; }

    /// <summary>
    /// Gets the <see cref="Azure.Identity.DefaultAzureCredential"/> shared by
    /// all exporters (signals) created from this options instance, so the
    /// credential chain is only probed once and its token cache is shared.
    /// </summary>
    internal TokenCredential SharedDefaultAzureCredential
        => LazyInitializer.EnsureInitialized(ref this.defaultAzureCredential, static () => new Azure.Identity.DefaultAzureCredential())!;

    internal static bool IsSystemManagedIdentity(string? managedIdentityId)
        => string.Equals(managedIdentityId?.Trim(), "system", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the options, mirroring the validation performed by the
    /// OpenTelemetry Collector Azure Data Explorer exporter.
    /// </summary>
    /// <exception cref="InvalidOperationException">The options are invalid.</exception>
    internal void Validate()
    {
        if (this.ClusterUri == null)
        {
            throw new InvalidOperationException("AzureDataExplorerExporterOptions.ClusterUri is mandatory.");
        }

        if (!this.ClusterUri.IsAbsoluteUri)
        {
            throw new InvalidOperationException($"AzureDataExplorerExporterOptions.ClusterUri '{this.ClusterUri}' is not a valid absolute URI.");
        }

        var authMethods = 0;
        if (!IsEmpty(this.ApplicationId) && !IsEmpty(this.ApplicationKey) && !IsEmpty(this.TenantId))
        {
            authMethods++;
        }

        if (!IsEmpty(this.ManagedIdentityId))
        {
            authMethods++;
        }

        if (this.UseAzureAuth)
        {
            authMethods++;
        }

        if (this.TokenCredential != null)
        {
            authMethods++;
        }

        if (authMethods != 1)
        {
            throw new InvalidOperationException(
                "Exactly one authentication method must be configured: either [ApplicationId, ApplicationKey, TenantId] or [ManagedIdentityId] or [UseAzureAuth] or [TokenCredential].");
        }

        if (this.IngestionType is not AzureDataExplorerIngestionType.Queued and not AzureDataExplorerIngestionType.Managed)
        {
            throw new InvalidOperationException(
                $"Unsupported IngestionType. Accepted types [{AzureDataExplorerIngestionType.Managed}, {AzureDataExplorerIngestionType.Queued}] Provided [{this.IngestionType}].");
        }

        if (!IsEmpty(this.ManagedIdentityId)
            && !IsSystemManagedIdentity(this.ManagedIdentityId)
            && !Guid.TryParse(this.ManagedIdentityId!.Trim(), out _))
        {
            throw new InvalidOperationException(
                "ManagedIdentityId should be a UUID string (for User Managed Identity) or system (for System Managed Identity).");
        }

        if (IsEmpty(this.DatabaseName))
        {
            throw new InvalidOperationException("AzureDataExplorerExporterOptions.DatabaseName is mandatory.");
        }

        if (this.RetryInitialInterval < TimeSpan.Zero || this.RetryMaxInterval < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Retry intervals must not be negative.");
        }
    }

    private static HttpClient DefaultHttpClientFactory() => KustoRestIngestor.DefaultHttpClient;

    private static bool IsEmpty(string? value) => string.IsNullOrWhiteSpace(value);
}
