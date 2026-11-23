// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// <see cref="IAzureDataExplorerIngestor"/> implementation that talks to the
/// Kusto REST API directly using <see cref="HttpClient"/>.
/// <para/>
/// Queued ingestion uploads each batch to a temporary blob container provided
/// by the cluster's data management service and posts an ingestion message to
/// its queue, as the Kusto SDK's queued ingest client does.
/// <para/>
/// Managed ingestion uses streaming ingestion, retrying transient failures and
/// falling back to queued ingestion when streaming is not possible, as the
/// Kusto SDK's managed streaming ingest client does.
/// </summary>
internal sealed class KustoRestIngestor : IAzureDataExplorerIngestor
{
    internal const string ConnectorName = "OpenTelemetry.Exporter.AzureDataExplorer";

    // Streaming ingestion requests are limited to 4 MB of uncompressed data;
    // larger payloads are queued, matching the Kusto SDK's managed client.
    internal const int MaxStreamingRawSize = 4 * 1024 * 1024;

    // One attempt plus three retries with exponential backoff, matching the Kusto SDK's managed client.
    internal const int MaxStreamingAttempts = 4;
    internal const string StorageApiVersion = "2021-08-06";

    private const string ManagementDatabase = "NetDefaultDB";
    private const int MaxResponseLength = 4 * 1024 * 1024;
    private const int MaxErrorBodyLength = 64 * 1024;

    private static readonly string ConnectorVersion = typeof(KustoRestIngestor).Assembly.GetPackageVersion();
    private static readonly Lazy<HttpClient> SharedHttpClient = new(CreateHttpClient, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly TimeSpan ResourcesLifetime = TimeSpan.FromHours(1);

    private readonly HttpClient httpClient;
    private readonly IKustoTokenProvider? tokenProvider;
    private readonly string databaseName;
    private readonly string tableName;
    private readonly string? mappingReference;
    private readonly AzureDataExplorerIngestionType ingestionType;
    private readonly string applicationName;
    private readonly object sync = new();
    private Task<IngestionResources>? resourcesTask;
    private DateTime resourcesExpiry;
    private long streamingSuspendedUntilTicks;
    private int disposed;

    public KustoRestIngestor(
        HttpClient httpClient,
        IKustoTokenProvider? tokenProvider,
        Uri clusterUri,
        string databaseName,
        string tableName,
        string? mappingReference,
        AzureDataExplorerIngestionType ingestionType,
        bool isManagedIdentity = false)
    {
        Guard.ThrowIfNull(httpClient);
        Guard.ThrowIfNull(clusterUri);

        this.httpClient = httpClient;
        this.tokenProvider = tokenProvider;
        this.EngineUri = GetEngineUri(clusterUri);
        this.IngestUri = GetIngestUri(clusterUri);
        this.databaseName = databaseName;
        this.tableName = tableName;
        this.mappingReference = string.IsNullOrWhiteSpace(mappingReference) ? null : mappingReference!.Trim();
        this.ingestionType = ingestionType;
        this.applicationName = GetApplicationName(isManagedIdentity);
    }

    internal enum StreamingFallback
    {
        /// <summary>Do not fall back to queued ingestion.</summary>
        None,

        /// <summary>Queue this batch.</summary>
        Queue,

        /// <summary>Queue this batch and stop streaming into the table for a while.</summary>
        Suspend,
    }

    internal static HttpClient DefaultHttpClient => SharedHttpClient.Value;

    internal Uri EngineUri { get; }

    internal Uri IngestUri { get; }

    /// <summary>
    /// Gets or sets the delay before the first streaming retry. It doubles on each retry.
    /// </summary>
    internal TimeSpan StreamingRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets how long to use queued ingestion after the cluster reports
    /// that streaming ingestion is not enabled for the table, matching the
    /// Kusto SDK's managed streaming ingestion policy.
    /// </summary>
    internal TimeSpan StreamingSuspensionPeriod { get; set; } = TimeSpan.FromMinutes(15);

    public static KustoRestIngestor Create(AzureDataExplorerExporterOptions options, string tableName, string? jsonMappingReference)
    {
        var httpClient = options.HttpClientFactory()
            ?? throw new InvalidOperationException("AzureDataExplorerExporterOptions.HttpClientFactory returned null.");
        var isManagedIdentity = !string.IsNullOrWhiteSpace(options.ManagedIdentityId);

        return new KustoRestIngestor(
            httpClient,
            new KustoTokenProvider(httpClient, authorityHost => CreateCredential(options, authorityHost)),
            options.ClusterUri!,
            options.DatabaseName,
            tableName,
            jsonMappingReference,
            options.IngestionType,
            isManagedIdentity);
    }

    public async Task IngestAsync(Stream data, CancellationToken cancellationToken)
    {
#if NET
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
#else
        if (Volatile.Read(ref this.disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(KustoRestIngestor));
        }
#endif

        var payload = Compress(data, out var uncompressedSize);

        if (this.ingestionType == AzureDataExplorerIngestionType.Managed)
        {
            if (uncompressedSize > MaxStreamingRawSize)
            {
                AzureDataExplorerExporterEventSource.Log.StreamingIngestionFallback(
                    this.tableName,
                    $"The payload size ({uncompressedSize.ToString(CultureInfo.InvariantCulture)} bytes) exceeds the streaming ingestion limit.");
            }
            else if (!this.IsStreamingSuspended() && await this.TryStreamAsync(payload, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        await this.QueueAsync(payload, uncompressedSize, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        // The HttpClient is shared (or owned by the caller) so there is nothing else to release.
        Interlocked.Exchange(ref this.disposed, 1);
    }

    internal static Uri GetEngineUri(Uri clusterUri)
    {
        var builder = new UriBuilder(clusterUri.GetLeftPart(UriPartial.Authority));
        if (builder.Host.StartsWith("ingest-", StringComparison.OrdinalIgnoreCase))
        {
            builder.Host = builder.Host.Substring("ingest-".Length);
        }

        return builder.Uri;
    }

    internal static Uri GetIngestUri(Uri clusterUri)
    {
        var builder = new UriBuilder(GetEngineUri(clusterUri));
        if (!IsLocalOrIpAddress(clusterUri))
        {
            builder.Host = "ingest-" + builder.Host;
        }

        return builder.Uri;
    }

    internal static TokenCredential CreateCredential(AzureDataExplorerExporterOptions options, Uri authorityHost)
    {
        // Precedence mirrors the OpenTelemetry Collector exporter, with the
        // .NET-specific TokenCredential option taking priority.
        if (options.TokenCredential != null)
        {
            return options.TokenCredential;
        }

        if (options.UseAzureAuth)
        {
            return options.SharedDefaultAzureCredential;
        }

        if (string.IsNullOrWhiteSpace(options.ManagedIdentityId))
        {
            return new ClientSecretCredential(
                options.TenantId,
                options.ApplicationId,
                options.ApplicationKey,
                new ClientSecretCredentialOptions { AuthorityHost = authorityHost });
        }

        return AzureDataExplorerExporterOptions.IsSystemManagedIdentity(options.ManagedIdentityId)
            ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
            : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityId!.Trim()));
    }

    internal static string GetApplicationName(bool isManagedIdentity)
        => $"Kusto.{ConnectorName}:{{{ConnectorVersion}}}|isManagedIdentity:{{{(isManagedIdentity ? "true" : "false")}}}";

    internal static AzureDataExplorerIngestionException CreateException(string operation, HttpStatusCode statusCode, string? body)
    {
        KustoError? error = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                error = JsonSerializer.Deserialize(body!, KustoJsonSerializerContext.Default.KustoErrorResponse)?.Error;
            }
            catch (JsonException)
            {
                // Not a Kusto error payload (for example a gateway or storage error).
            }
        }

        var status = (int)statusCode;
        var isTransientStatus = status is 408 or 429 or >= 500;
        var isPermanent = !isTransientStatus && (error?.Permanent ?? status is >= 400 and < 500);

        var detail = error?.DetailedMessage ?? error?.Message ?? Truncate(body);
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"{operation} failed with HTTP {status} ({statusCode})."
            : $"{operation} failed with HTTP {status} ({statusCode}): {detail}";

        return new AzureDataExplorerIngestionException(message, isPermanent, statusCode, error?.Type ?? error?.Code, failureSubCode: error?.Code);
    }

    internal static StreamingFallback GetStreamingFallback(Exception exception)
    {
        if (exception is not AzureDataExplorerIngestionException ingestionException)
        {
            return StreamingFallback.None;
        }

        var text = ingestionException.FailureSubCode + " " + ingestionException.ErrorCode + " " + ingestionException.Message;

        // Streaming ingestion is disabled on the cluster or not enabled on the
        // database/table. Like the Kusto SDK's managed client, stop trying to
        // stream into the table for a while.
        if (ContainsIgnoreCase(text, "StreamingIngestionPolicyNotEnabled")
            || ContainsIgnoreCase(text, "StreamingIngestionDisabled"))
        {
            return StreamingFallback.Suspend;
        }

        // Errors that streaming ingestion cannot handle but queued ingestion can,
        // matching the Kusto SDK's managed client.
        var queue = ingestionException.StatusCode == HttpStatusCode.RequestEntityTooLarge
            || ContainsIgnoreCase(text, "StreamingIngestionRequestTooLarge")
            || ContainsIgnoreCase(text, "StreamingIngestionNotSupported")
            || ContainsIgnoreCase(text, "StreamingIngestionUpdatePolicyIncompatible")
            || ContainsIgnoreCase(text, "QuerySchemaDoesNotMatchTableSchema")
            || ContainsIgnoreCase(text, "FileTooLarge")
            || ContainsIgnoreCase(text, "InputStreamTooLarge");

        return queue ? StreamingFallback.Queue : StreamingFallback.None;
    }

    internal static IngestionResources ParseIngestionResources(KustoV1Table table, string authorizationContext)
    {
        var typeIndex = GetColumnIndex(table, "ResourceTypeName");
        var rootIndex = GetColumnIndex(table, "StorageRoot");
        var queues = new List<Uri>();
        var containers = new List<Uri>();

        foreach (var row in table.Rows ?? [])
        {
            if (row.Count <= Math.Max(typeIndex, rootIndex))
            {
                continue;
            }

            var type = row[typeIndex].ValueKind == JsonValueKind.String ? row[typeIndex].GetString() : null;
            var root = row[rootIndex].ValueKind == JsonValueKind.String ? row[rootIndex].GetString() : null;
            if (root == null || !Uri.TryCreate(root, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (string.Equals(type, "SecuredReadyForAggregationQueue", StringComparison.OrdinalIgnoreCase))
            {
                queues.Add(uri);
            }
            else if (string.Equals(type, "TempStorage", StringComparison.OrdinalIgnoreCase))
            {
                containers.Add(uri);
            }
        }

        if (queues.Count == 0 || containers.Count == 0)
        {
            throw new AzureDataExplorerIngestionException(
                "The data management service did not return any ingestion queues or temporary storage containers.",
                isPermanent: false);
        }

        return new IngestionResources(queues.ToArray(), containers.ToArray(), authorizationContext);
    }

    internal static Uri GetBlobUri(Uri containerUri, string blobName)
    {
        var builder = new UriBuilder(containerUri);
        builder.Path = builder.Path.TrimEnd('/') + "/" + Uri.EscapeDataString(blobName);
        return builder.Uri;
    }

    internal static Uri GetQueueMessagesUri(Uri queueUri)
    {
        var builder = new UriBuilder(queueUri);
        builder.Path = builder.Path.TrimEnd('/') + "/messages";
        return builder.Uri;
    }

    // CA2000: HttpClient owns and disposes the handler.
    // CA5399: the handler's default certificate revocation checking is intentionally left unchanged.
#pragma warning disable CA5399, CA2000
    private static HttpClient CreateHttpClient()
    {
#if NET
        var handler = new SocketsHttpHandler
        {
            // Pick up DNS changes for long-lived exporters.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
#else
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
#endif

#if NET
        return new HttpClient(handler, disposeHandler: true);
#else
        return new HttpClient(new ConnectionLeaseHandler(handler), disposeHandler: true);
#endif
    }
#pragma warning restore CA5399, CA2000

    private static bool ContainsIgnoreCase(string value, string substring)
#if NET
        => value.Contains(substring, StringComparison.OrdinalIgnoreCase);
#else
        => value.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0;
#endif

    private static bool IsLocalOrIpAddress(Uri uri)
        => uri.IsLoopback
            || uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

    private static byte[] Compress(Stream data, out long uncompressedSize)
    {
        using var output = new MemoryStream();
        uncompressedSize = 0;
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = data.Read(buffer, 0, buffer.Length)) > 0)
            {
                gzip.Write(buffer, 0, read);
                uncompressedSize += read;
            }
        }

        return output.ToArray();
    }

    private static int GetColumnIndex(KustoV1Table table, string columnName)
    {
        var columns = table.Columns ?? [];
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i].ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new AzureDataExplorerIngestionException(
            $"The management command response did not contain the '{columnName}' column.",
            isPermanent: false);
    }

    private static async Task<string?> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await HttpClientHelpers.GetResponseBodyAsByteArrayAsync(MaxErrorBodyLength, response, cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static string? Truncate(string? value)
        => value == null || value.Length <= 1024 ? value : new StringBuilder(1027).Append(value, 0, 1024).Append("...").ToString();

    private bool IsStreamingSuspended()
        => DateTime.UtcNow.Ticks < Interlocked.Read(ref this.streamingSuspendedUntilTicks);

    private async Task<bool> TryStreamAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var delay = this.StreamingRetryDelay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await this.StreamAsync(payload, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (GetStreamingFallback(ex) != StreamingFallback.None)
            {
                AzureDataExplorerExporterEventSource.Log.StreamingIngestionFallback(this.tableName, ex);
                if (GetStreamingFallback(ex) == StreamingFallback.Suspend && this.StreamingSuspensionPeriod > TimeSpan.Zero)
                {
                    Interlocked.Exchange(ref this.streamingSuspendedUntilTicks, (DateTime.UtcNow + this.StreamingSuspensionPeriod).Ticks);
                    AzureDataExplorerExporterEventSource.Log.StreamingIngestionSuspended(this.tableName, (int)this.StreamingSuspensionPeriod.TotalMinutes);
                }

                return false;
            }
            catch (Exception ex) when (!AzureDataExplorerIngestionException.IsPermanentError(ex) && !cancellationToken.IsCancellationRequested)
            {
                if (attempt >= MaxStreamingAttempts)
                {
                    AzureDataExplorerExporterEventSource.Log.StreamingIngestionFallback(this.tableName, ex);
                    return false;
                }

                AzureDataExplorerExporterEventSource.Log.StreamingIngestionRetrying(this.tableName, attempt, ex);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = TimeSpan.FromTicks(delay.Ticks * 2);
                }
            }
        }
    }

    private async Task StreamAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var query = new StringBuilder("streamFormat=json");
        if (this.mappingReference != null)
        {
            query.Append("&mappingName=").Append(Uri.EscapeDataString(this.mappingReference));
        }

        var uri = new Uri(
            this.EngineUri,
            $"v1/rest/ingest/{Uri.EscapeDataString(this.databaseName)}/{Uri.EscapeDataString(this.tableName)}?{query}");

        using var response = await this.SendKustoRequestAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new ByteArrayContent(payload),
                };

                request.Content.Headers.ContentEncoding.Add("gzip");
                return request;
            },
            "KNC.executeStreamingIngest",
            "Streaming ingestion",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task QueueAsync(byte[] payload, long rawSize, CancellationToken cancellationToken)
    {
        var resources = await this.GetIngestionResourcesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var id = Guid.NewGuid().ToString();
            var blobName = $"{this.databaseName}__{this.tableName}__{id}__otel.json.gz";
            var blobUri = await this.UploadBlobAsync(resources, blobName, payload, cancellationToken).ConfigureAwait(false);

            var additionalProperties = new Dictionary<string, string>
            {
                ["authorizationContext"] = resources.AuthorizationContext,
                ["format"] = "json",
            };

            if (this.mappingReference != null)
            {
                additionalProperties["ingestionMappingReference"] = this.mappingReference;
                additionalProperties["ingestionMappingType"] = "Json";
            }

            var message = new KustoQueuedIngestionMessage
            {
                Id = id,
                BlobPath = blobUri.AbsoluteUri,
                RawDataSize = rawSize,
                DatabaseName = this.databaseName,
                TableName = this.tableName,
                RetainBlobOnSuccess = true,
                FlushImmediately = false,
                IgnoreSizeLimit = false,
                ReportLevel = 0, // FailuresOnly
                ReportMethod = 0, // Queue
                SourceMessageCreationTime = DateTime.UtcNow,
                AdditionalProperties = additionalProperties,
            };

            await this.EnqueueAsync(resources, message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Storage credentials may have been rotated; refresh resources on the next attempt.
            this.InvalidateIngestionResources();
            throw;
        }
    }

    private async Task<Uri> UploadBlobAsync(IngestionResources resources, string blobName, byte[] payload, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var i = 0; i < resources.Containers.Length; i++)
        {
            var blobUri = GetBlobUri(resources.NextContainer(), blobName);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, blobUri)
                {
                    Content = new ByteArrayContent(payload),
                };

                request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
                request.Headers.TryAddWithoutValidation("x-ms-version", StorageApiVersion);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                using var response = await this.SendStorageRequestAsync(request, "Blob upload", cancellationToken).ConfigureAwait(false);
                return blobUri;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
            }
        }

        throw new AzureDataExplorerIngestionException(
            "Failed to upload the data to any of the temporary storage containers.",
            isPermanent: false,
            innerException: lastError);
    }

    private async Task EnqueueAsync(IngestionResources resources, KustoQueuedIngestionMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, KustoJsonSerializerContext.Default.KustoQueuedIngestionMessage);
        var body = $"<QueueMessage><MessageText>{Convert.ToBase64String(json)}</MessageText></QueueMessage>";

        Exception? lastError = null;
        for (var i = 0; i < resources.Queues.Length; i++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, GetQueueMessagesUri(resources.NextQueue()))
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/xml"),
                };

                request.Headers.TryAddWithoutValidation("x-ms-version", StorageApiVersion);

                using var response = await this.SendStorageRequestAsync(request, "Posting the ingestion message", cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
            }
        }

        throw new AzureDataExplorerIngestionException(
            "Failed to post the ingestion message to any of the ingestion queues.",
            isPermanent: false,
            innerException: lastError);
    }

    private Task<IngestionResources> GetIngestionResourcesAsync(CancellationToken cancellationToken)
    {
        lock (this.sync)
        {
            var task = this.resourcesTask;
            if (task == null
                || task.IsFaulted
                || task.IsCanceled
                || (task.Status == TaskStatus.RanToCompletion && this.resourcesExpiry <= DateTime.UtcNow))
            {
                this.resourcesExpiry = DateTime.UtcNow + ResourcesLifetime;
                task = this.resourcesTask = this.FetchIngestionResourcesAsync(cancellationToken);
            }

            return task;
        }
    }

    private void InvalidateIngestionResources()
    {
        lock (this.sync)
        {
            this.resourcesTask = null;
        }
    }

    private async Task<IngestionResources> FetchIngestionResourcesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await this.FetchIngestionResourcesCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AzureDataExplorerIngestionException ex) when (ex.IsPermanent)
        {
            // The Kusto SDK's queued ingest client wraps failures to retrieve the
            // ingestion resources and identity token (including 401 and 403
            // responses from the data management service) in non-permanent
            // exceptions, so exports keep retrying them.
            throw new AzureDataExplorerIngestionException(ex.Message, isPermanent: false, ex.StatusCode, ex.ErrorCode, ex, ex.FailureSubCode);
        }
    }

    private async Task<IngestionResources> FetchIngestionResourcesCoreAsync(CancellationToken cancellationToken)
    {
        var resources = await this.ExecuteManagementCommandAsync(".get ingestion resources", cancellationToken).ConfigureAwait(false);
        var identity = await this.ExecuteManagementCommandAsync(".get kusto identity token", cancellationToken).ConfigureAwait(false);

        var index = GetColumnIndex(identity, "AuthorizationContext");
        var row = identity.Rows?.FirstOrDefault();
        var authorizationContext = row != null && row.Count > index && row[index].ValueKind == JsonValueKind.String
            ? row[index].GetString()
            : null;

        if (string.IsNullOrEmpty(authorizationContext))
        {
            throw new AzureDataExplorerIngestionException(
                "The data management service did not return a Kusto identity token.",
                isPermanent: false);
        }

        return ParseIngestionResources(resources, authorizationContext!);
    }

    private async Task<KustoV1Table> ExecuteManagementCommandAsync(string command, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new KustoManagementRequest { Database = ManagementDatabase, Command = command },
            KustoJsonSerializerContext.Default.KustoManagementRequest);

        var uri = new Uri(this.IngestUri, "v1/rest/mgmt");

        using var response = await this.SendKustoRequestAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new ByteArrayContent(body),
                };

                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
                return request;
            },
            "KNC.execute",
            $"Management command '{command}'",
            cancellationToken).ConfigureAwait(false);

        var bytes = await HttpClientHelpers.GetResponseBodyAsByteArrayAsync(MaxResponseLength, response, cancellationToken).ConfigureAwait(false);
        KustoV1Response? result;
        try
        {
            result = JsonSerializer.Deserialize(bytes, KustoJsonSerializerContext.Default.KustoV1Response);
        }
        catch (JsonException ex)
        {
            throw new AzureDataExplorerIngestionException(
                $"The response to management command '{command}' was not valid JSON.",
                isPermanent: false,
                response.StatusCode,
                innerException: ex);
        }

        return result?.Tables?.FirstOrDefault()
            ?? throw new AzureDataExplorerIngestionException(
                $"The response to management command '{command}' did not contain any tables.",
                isPermanent: false,
                response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendKustoRequestAsync(Func<HttpRequestMessage> createRequest, string clientActivity, string operation, CancellationToken cancellationToken)
    {
        string? token = null;
        for (var attempt = 1; ; attempt++)
        {
            using var request = createRequest();

            if (this.tokenProvider != null && attempt == 1)
            {
                token = await this.tokenProvider.GetTokenAsync(request.RequestUri!, cancellationToken).ConfigureAwait(false);
            }

            if (token != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("x-ms-app", this.applicationName);
            request.Headers.TryAddWithoutValidation("x-ms-client-version", $"Kusto.{ConnectorName}:{ConnectorVersion}");
            request.Headers.TryAddWithoutValidation("x-ms-client-request-id", $"{clientActivity};{Guid.NewGuid()}");

            var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            using (response)
            {
                // A cached token may have been revoked or invalidated (for example
                // after a role assignment change). Discard it and retry once if the
                // credential issues a different token; otherwise report the failure.
                if (response.StatusCode == HttpStatusCode.Unauthorized && token != null && attempt == 1)
                {
                    this.tokenProvider!.InvalidateToken(token);
                    var newToken = await this.tokenProvider.GetTokenAsync(request.RequestUri!, cancellationToken).ConfigureAwait(false);
                    if (newToken != null && !string.Equals(newToken, token, StringComparison.Ordinal))
                    {
                        token = newToken;
                        AzureDataExplorerExporterEventSource.Log.UnauthorizedRequestRetrying(operation);
                        continue;
                    }
                }

                var body = await ReadErrorBodyAsync(response, cancellationToken).ConfigureAwait(false);
                throw CreateException(operation, response.StatusCode, body);
            }
        }
    }

    private Task<HttpResponseMessage> SendStorageRequestAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("x-ms-client-request-id", Guid.NewGuid().ToString());
        return this.SendAsync(request, operation, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var body = await ReadErrorBodyAsync(response, cancellationToken).ConfigureAwait(false);
            throw CreateException(operation, response.StatusCode, body);
        }
    }

    internal sealed class IngestionResources
    {
        private int queueIndex = -1;
        private int containerIndex = -1;

        public IngestionResources(Uri[] queues, Uri[] containers, string authorizationContext)
        {
            this.Queues = queues;
            this.Containers = containers;
            this.AuthorizationContext = authorizationContext;

            // Start at a random account so multiple exporters spread their load.
            var start = Guid.NewGuid().GetHashCode() & int.MaxValue;
            this.queueIndex = start - 1;
            this.containerIndex = start - 1;
        }

        public Uri[] Queues { get; }

        public Uri[] Containers { get; }

        public string AuthorizationContext { get; }

        public Uri NextQueue() => this.Queues[(int)((uint)Interlocked.Increment(ref this.queueIndex) % (uint)this.Queues.Length)];

        public Uri NextContainer() => this.Containers[(int)((uint)Interlocked.Increment(ref this.containerIndex) % (uint)this.Containers.Length)];
    }
}
