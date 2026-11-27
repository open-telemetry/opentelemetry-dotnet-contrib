// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Text;
using System.Text.Json;
using Azure.Core;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

public class KustoRestIngestorTests
{
    private const string Database = "oteldb";
    private const string Table = "OTELLogs";
    private const string Payload = "{\"Body\":\"hello\"}\n{\"Body\":\"world\"}\n";

    private const string IngestionResourcesResponse =
        """
        {"Tables":[{"TableName":"Table_0","Columns":[{"ColumnName":"ResourceTypeName","DataType":"String"},{"ColumnName":"StorageRoot","DataType":"String"}],"Rows":[
        ["SecuredReadyForAggregationQueue","https://queue1.queue.core.windows.net/readyforaggregation-secured?sv=q1"],
        ["FailedIngestionsQueue","https://queue1.queue.core.windows.net/failedingestions?sv=f1"],
        ["TempStorage","https://storage1.blob.core.windows.net/tempcontainer?sv=s1"],
        ["TempStorage","https://storage2.blob.core.windows.net/tempcontainer?sv=s2"]]}]}
        """;

    private const string IdentityTokenResponse =
        """
        {"Tables":[{"TableName":"Table_0","Columns":[{"ColumnName":"AuthorizationContext","DataType":"String"}],"Rows":[["auth-context"]]}]}
        """;

    private static readonly Uri ClusterUri = new("https://mycluster.westus.kusto.windows.net");

    [Fact]
    public async Task Managed_StreamsCompressedPayloadToEngine()
    {
        using var handler = new FakeHandler(_ => Respond(HttpStatusCode.OK));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed, mapping: "my mapping");

        await IngestAsync(ingestor, Payload);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://mycluster.westus.kusto.windows.net/v1/rest/ingest/oteldb/OTELLogs?streamFormat=json&mappingName=my%20mapping",
            request.Uri.AbsoluteUri);
        Assert.Equal("Bearer test-token", request.Headers["Authorization"]);
        Assert.Equal("gzip", request.Headers["Content-Encoding"]);
        Assert.StartsWith("Kusto.OpenTelemetry.Exporter.AzureDataExplorer:", request.Headers["x-ms-app"], StringComparison.Ordinal);
        Assert.StartsWith("KNC.executeStreamingIngest;", request.Headers["x-ms-client-request-id"], StringComparison.Ordinal);
        Assert.Equal(Payload, Decompress(request.Body));
    }

    [Fact]
    public async Task Managed_StreamsWithoutMappingOrAuthorization()
    {
        using var handler = new FakeHandler(_ => Respond(HttpStatusCode.OK));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed, authenticate: false);

        await IngestAsync(ingestor, Payload);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://mycluster.westus.kusto.windows.net/v1/rest/ingest/oteldb/OTELLogs?streamFormat=json", request.Uri.AbsoluteUri);
        Assert.False(request.Headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task Managed_RetriesTransientErrorsThenFallsBackToQueued()
    {
        using var handler = new FakeHandler(request => IsStreaming(request) ? Respond(HttpStatusCode.ServiceUnavailable) : RespondToQueuedFlow(request));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);

        await IngestAsync(ingestor, Payload);

        Assert.Equal(KustoRestIngestor.MaxStreamingAttempts, handler.Requests.Count(IsStreaming));
        Assert.Single(handler.Requests, r => r.Method == HttpMethod.Put);
        Assert.Single(handler.Requests, IsQueueMessage);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":"BadRequest","@type":"Kusto.DataNode.Exceptions.StreamingIngestionPolicyNotEnabledException","@message":"Streaming ingestion policy is not enabled","@permanent":true}}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":"BadRequest","@type":"Kusto.Common.Svc.Exceptions.StreamingIngestionDisabledException","@permanent":true}}""")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, null)]
    public async Task Managed_FallsBackToQueuedWhenStreamingIsUnavailable(HttpStatusCode statusCode, string? body)
    {
        using var handler = new FakeHandler(request => IsStreaming(request) ? Respond(statusCode, body) : RespondToQueuedFlow(request));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);

        await IngestAsync(ingestor, Payload);

        Assert.Single(handler.Requests, IsStreaming);
        Assert.Single(handler.Requests, IsQueueMessage);
    }

    [Fact]
    public async Task Managed_PermanentErrorIsNotRetriedOrQueued()
    {
        using var handler = new FakeHandler(_ => Respond(
            HttpStatusCode.BadRequest,
            """{"error":{"code":"BadRequest","@type":"Kusto.Data.Exceptions.EntityNotFoundException","@message":"Table 'OTELLogs' was not found","@permanent":true}}"""));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);

        var exception = await Assert.ThrowsAsync<AzureDataExplorerIngestionException>(() => IngestAsync(ingestor, Payload));

        Assert.True(exception.IsPermanent);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("Table 'OTELLogs' was not found", exception.Message, StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Managed_LargePayloadIsQueuedWithoutStreaming()
    {
        using var handler = new FakeHandler(RespondToQueuedFlow);
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);

        var payload = new string('a', KustoRestIngestor.MaxStreamingRawSize + 1);
        await IngestAsync(ingestor, payload);

        Assert.DoesNotContain(handler.Requests, IsStreaming);
        var message = GetQueuedMessage(Assert.Single(handler.Requests, IsQueueMessage));
        Assert.Equal(payload.Length, message.GetProperty("RawDataSize").GetInt64());
    }

    [Fact]
    public async Task Queued_UploadsBlobAndPostsIngestionMessage()
    {
        using var handler = new FakeHandler(RespondToQueuedFlow);
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Queued, mapping: "my_mapping");

        await IngestAsync(ingestor, Payload);

        var commands = handler.Requests.Where(r => r.Uri.AbsolutePath == "/v1/rest/mgmt").ToList();
        Assert.Equal(2, commands.Count);
        Assert.All(commands, c => Assert.Equal("https://ingest-mycluster.westus.kusto.windows.net/v1/rest/mgmt", c.Uri.AbsoluteUri));
        Assert.All(commands, c => Assert.Equal("Bearer test-token", c.Headers["Authorization"]));
        Assert.Contains(commands, c => GetCommand(c) == ".get ingestion resources");
        Assert.Contains(commands, c => GetCommand(c) == ".get kusto identity token");

        var upload = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Put);
        Assert.Equal("BlockBlob", upload.Headers["x-ms-blob-type"]);
        Assert.False(upload.Headers.ContainsKey("Authorization"));
        Assert.EndsWith(".blob.core.windows.net", upload.Uri.Host, StringComparison.Ordinal);
        Assert.StartsWith("/tempcontainer/oteldb__OTELLogs__", upload.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("__otel.json.gz", upload.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.StartsWith("?sv=s", upload.Uri.Query, StringComparison.Ordinal);
        Assert.Equal(Payload, Decompress(upload.Body));

        var queueRequest = Assert.Single(handler.Requests, IsQueueMessage);
        Assert.Equal("https://queue1.queue.core.windows.net/readyforaggregation-secured/messages?sv=q1", queueRequest.Uri.AbsoluteUri);

        var message = GetQueuedMessage(queueRequest);
        Assert.Equal(upload.Uri.AbsoluteUri, message.GetProperty("BlobPath").GetString());
        Assert.Equal(Database, message.GetProperty("DatabaseName").GetString());
        Assert.Equal(Table, message.GetProperty("TableName").GetString());
        Assert.Equal(Encoding.UTF8.GetByteCount(Payload), message.GetProperty("RawDataSize").GetInt64());
        Assert.True(Guid.TryParse(message.GetProperty("Id").GetString(), out _));

        var properties = message.GetProperty("AdditionalProperties");
        Assert.Equal("auth-context", properties.GetProperty("authorizationContext").GetString());
        Assert.Equal("json", properties.GetProperty("format").GetString());
        Assert.Equal("my_mapping", properties.GetProperty("ingestionMappingReference").GetString());
        Assert.Equal("Json", properties.GetProperty("ingestionMappingType").GetString());
    }

    [Fact]
    public async Task Managed_SuspendsStreamingAfterPolicyNotEnabled()
    {
        using var handler = new FakeHandler(request => IsStreaming(request)
            ? Respond(HttpStatusCode.BadRequest, """{"error":{"code":"BadRequest_StreamingIngestionPolicyNotEnabled","@permanent":true}}""")
            : RespondToQueuedFlow(request));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);

        await IngestAsync(ingestor, Payload);
        await IngestAsync(ingestor, Payload);

        Assert.Single(handler.Requests, IsStreaming);
        Assert.Equal(2, handler.Requests.Count(IsQueueMessage));

        using var resumingIngestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);
        resumingIngestor.StreamingSuspensionPeriod = TimeSpan.Zero;

        await IngestAsync(resumingIngestor, Payload);
        await IngestAsync(resumingIngestor, Payload);

        Assert.Equal(3, handler.Requests.Count(IsStreaming));
    }

    [Fact]
    public async Task Managed_TooLargeFallbackDoesNotSuspendStreaming()
    {
        using var handler = new FakeHandler(request => IsStreaming(request) ? Respond(HttpStatusCode.RequestEntityTooLarge) : RespondToQueuedFlow(request));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);

        await IngestAsync(ingestor, Payload);
        await IngestAsync(ingestor, Payload);

        Assert.Equal(2, handler.Requests.Count(IsStreaming));
    }

    [Theory]
    [InlineData("BadRequest_StreamingIngestionPolicyNotEnabled", "Suspend")]
    [InlineData("NotFound_StreamingIngestionDisabledForCluster", "Suspend")]
    [InlineData("BadRequest_StreamingIngestionUpdatePolicyIncompatible", "Queue")]
    [InlineData("UpdatePolicy_QuerySchemaDoesNotMatchTableSchema", "Queue")]
    [InlineData("BadRequest_FileTooLarge", "Queue")]
    [InlineData("Stream_InputStreamTooLarge", "Queue")]
    [InlineData("BadRequest_EntityNotFound", "None")]
    public void GetStreamingFallback_MatchesKustoSdkErrorCodes(string code, string expected)
    {
        var exception = KustoRestIngestor.CreateException("Streaming ingestion", HttpStatusCode.BadRequest, $$$"""{"error":{"code":"{{{code}}}","@permanent":true}}""");

        Assert.Equal(code, exception.FailureSubCode);
        Assert.Equal(expected, KustoRestIngestor.GetStreamingFallback(exception).ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Managed_StreamingAuthorizationFailureIsPermanent(HttpStatusCode statusCode)
    {
        using var handler = new FakeHandler(_ => Respond(statusCode));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);

        var exception = await Assert.ThrowsAsync<AzureDataExplorerIngestionException>(() => IngestAsync(ingestor, Payload));

        Assert.True(exception.IsPermanent);
        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Unauthorized_RetriesOnceWithNewToken()
    {
        var tokenProvider = new StaticTokenProvider { RotateOnInvalidate = true };
        using var handler = new FakeHandler(request =>
            request.Headers["Authorization"] == "Bearer test-token" ? Respond(HttpStatusCode.Unauthorized) : Respond(HttpStatusCode.OK));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed, tokenProvider: tokenProvider);

        await IngestAsync(ingestor, Payload);

        Assert.Equal(["test-token"], tokenProvider.InvalidatedTokens);
        Assert.Equal(["Bearer test-token", "Bearer test-token-2"], handler.Requests.Select(r => r.Headers["Authorization"]));
        Assert.Equal(Payload, Decompress(handler.Requests[1].Body));
    }

    [Fact]
    public async Task Unauthorized_IsNotRetriedWhenCredentialReturnsSameToken()
    {
        var tokenProvider = new StaticTokenProvider();
        using var handler = new FakeHandler(_ => Respond(HttpStatusCode.Unauthorized));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed, tokenProvider: tokenProvider);

        await Assert.ThrowsAsync<AzureDataExplorerIngestionException>(() => IngestAsync(ingestor, Payload));

        Assert.Equal(["test-token"], tokenProvider.InvalidatedTokens);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Queued_DataManagementFailuresAreNotPermanent(HttpStatusCode statusCode)
    {
        var fail = true;
        using var handler = new FakeHandler(request =>
            fail && request.Uri.AbsolutePath == "/v1/rest/mgmt" ? Respond(statusCode) : RespondToQueuedFlow(request));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Queued);

        var exception = await Assert.ThrowsAsync<AzureDataExplorerIngestionException>(() => IngestAsync(ingestor, Payload));
        Assert.False(exception.IsPermanent);
        Assert.Equal(statusCode, exception.StatusCode);
        Assert.False(AzureDataExplorerIngestionException.IsPermanentError(exception));

        fail = false;
        await IngestAsync(ingestor, Payload);
        Assert.Single(handler.Requests, IsQueueMessage);
    }

    [Fact]
    public async Task Queued_TriesNextContainerWhenUploadFails()
    {
        var uploads = 0;
        using var handler = new FakeHandler(request =>
            request.Method == HttpMethod.Put && Interlocked.Increment(ref uploads) == 1
                ? Respond(HttpStatusCode.Forbidden)
                : RespondToQueuedFlow(request));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Queued);

        await IngestAsync(ingestor, Payload);

        var attempts = handler.Requests.Where(r => r.Method == HttpMethod.Put).ToList();
        Assert.Equal(2, attempts.Count);
        Assert.NotEqual(attempts[0].Uri.Host, attempts[1].Uri.Host);
        Assert.Equal(attempts[1].Uri.AbsoluteUri, GetQueuedMessage(Assert.Single(handler.Requests, IsQueueMessage)).GetProperty("BlobPath").GetString());
    }

    [Fact]
    public async Task Queued_CachesIngestionResourcesAndRefreshesAfterFailure()
    {
        var failQueue = false;
        using var handler = new FakeHandler(request =>
            failQueue && IsQueueMessage(request) ? Respond(HttpStatusCode.InternalServerError) : RespondToQueuedFlow(request));
        using var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Queued);

        await IngestAsync(ingestor, Payload);
        await IngestAsync(ingestor, Payload);
        Assert.Equal(2, CountCommands(handler));

        failQueue = true;
        var exception = await Assert.ThrowsAsync<AzureDataExplorerIngestionException>(() => IngestAsync(ingestor, Payload));
        Assert.False(exception.IsPermanent);

        failQueue = false;
        await IngestAsync(ingestor, Payload);
        Assert.Equal(4, CountCommands(handler));
    }

    [Fact]
    public async Task IngestAfterDispose_Throws()
    {
        using var handler = new FakeHandler(_ => Respond(HttpStatusCode.OK));
        var ingestor = CreateIngestor(handler, AzureDataExplorerIngestionType.Managed);
        ingestor.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => IngestAsync(ingestor, Payload));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData((HttpStatusCode)429, null, false)]
    [InlineData(HttpStatusCode.RequestTimeout, null, false)]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":{"@permanent":true}}""", false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "<html>gateway</html>", false)]
    [InlineData(HttpStatusCode.BadRequest, null, true)]
    [InlineData(HttpStatusCode.Unauthorized, null, true)]
    [InlineData(HttpStatusCode.Forbidden, "not json", true)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"@permanent":false}}""", false)]
    public void CreateException_ClassifiesErrors(HttpStatusCode statusCode, string? body, bool expectedPermanent)
    {
        var exception = KustoRestIngestor.CreateException("Test", statusCode, body);

        Assert.Equal(expectedPermanent, exception.IsPermanent);
        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Equal(expectedPermanent, AzureDataExplorerIngestionException.IsPermanentError(exception));
    }

    [Fact]
    public void CreateException_UsesKustoErrorDetails()
    {
        var exception = KustoRestIngestor.CreateException(
            "Streaming ingestion",
            HttpStatusCode.BadRequest,
            """{"error":{"code":"BadRequest","message":"Request is invalid","@type":"Kusto.Data.Exceptions.SyntaxException","@message":"Detailed message"}}""");

        Assert.Equal("Kusto.Data.Exceptions.SyntaxException", exception.ErrorCode);
        Assert.Equal("Streaming ingestion failed with HTTP 400 (BadRequest): Detailed message", exception.Message);
    }

    [Fact]
    public void ParseIngestionResources_RequiresQueuesAndContainers()
    {
        var table = JsonSerializer.Deserialize(
            """{"Tables":[{"Columns":[{"ColumnName":"ResourceTypeName"},{"ColumnName":"StorageRoot"}],"Rows":[["TempStorage","https://s.blob.core.windows.net/c?sv=1"]]}]}""",
            KustoJsonSerializerContext.Default.KustoV1Response)!.Tables![0];

        var exception = Assert.Throws<AzureDataExplorerIngestionException>(() => KustoRestIngestor.ParseIngestionResources(table, "ctx"));
        Assert.False(exception.IsPermanent);
    }

    [Fact]
    public async Task TokenProvider_UsesClusterMetadataAndCachesTokens()
    {
        using var handler = new FakeHandler(_ => Respond(
            HttpStatusCode.OK,
            """{"AzureAD":{"LoginEndpoint":"https://login.microsoftonline.us","LoginMfaRequired":false,"KustoClientAppId":"app","KustoServiceResourceId":"https://kusto.usgovcloudapi.net"}}"""));
        using var httpClient = new HttpClient(handler);
        var credential = new FakeCredential();
        Uri? authorityHost = null;
        var provider = new KustoTokenProvider(httpClient, host =>
        {
            authorityHost = host;
            return credential;
        });

        Assert.Equal("token-1", await provider.GetTokenAsync(new Uri("https://mycluster.kusto.usgovcloudapi.net/v1/rest/mgmt"), CancellationToken.None));
        Assert.Equal("token-1", await provider.GetTokenAsync(new Uri("https://mycluster.kusto.usgovcloudapi.net/v1/rest/ingest/db/table"), CancellationToken.None));

        Assert.Equal(new Uri("https://login.microsoftonline.us"), authorityHost);
        Assert.Equal(["https://kusto.usgovcloudapi.net/.default"], credential.Scopes);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://mycluster.kusto.usgovcloudapi.net/v1/rest/auth/metadata", request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task TokenProvider_FallsBackToPublicCloudWhenMetadataIsMissing()
    {
        using var handler = new FakeHandler(_ => Respond(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler);
        var credential = new FakeCredential();
        Uri? authorityHost = null;
        var provider = new KustoTokenProvider(httpClient, host =>
        {
            authorityHost = host;
            return credential;
        });

        await provider.GetTokenAsync(ClusterUri, CancellationToken.None);

        Assert.Equal(new Uri(KustoTokenProvider.DefaultLoginEndpoint), authorityHost);
        Assert.Equal([KustoTokenProvider.DefaultKustoResourceId + "/.default"], credential.Scopes);
    }

    [Fact]
    public async Task TokenProvider_RefreshesExpiringTokens()
    {
        using var handler = new FakeHandler(_ => Respond(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler);
        var credential = new FakeCredential { Lifetime = TimeSpan.FromMinutes(1) };
        var provider = new KustoTokenProvider(httpClient, _ => credential);

        Assert.Equal("token-1", await provider.GetTokenAsync(ClusterUri, CancellationToken.None));
        Assert.Equal("token-2", await provider.GetTokenAsync(ClusterUri, CancellationToken.None));
    }

    [Fact]
    public async Task TokenProvider_InvalidateTokenAcquiresNewToken()
    {
        using var handler = new FakeHandler(_ => Respond(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(handler);
        var provider = new KustoTokenProvider(httpClient, _ => new FakeCredential());

        Assert.Equal("token-1", await provider.GetTokenAsync(ClusterUri, CancellationToken.None));
        provider.InvalidateToken("other-token");
        Assert.Equal("token-1", await provider.GetTokenAsync(ClusterUri, CancellationToken.None));
        provider.InvalidateToken("token-1");
        Assert.Equal("token-2", await provider.GetTokenAsync(ClusterUri, CancellationToken.None));
    }

    [Fact]
    public void TokenProvider_UsesMfaResourceWhenRequired()
    {
        var settings = KustoTokenProvider.ParseAuthSettings(new KustoAzureAdMetadata
        {
            LoginMfaRequired = true,
            KustoServiceResourceId = "https://kusto.kusto.windows.net/",
        });

        Assert.Equal("https://kusto.kustomfa.windows.net/.default", settings.Scope);
    }

    [Fact]
    public async Task Create_UsesHttpClientFromFactory()
    {
        using var handler = new FakeHandler(request => request.Uri.AbsolutePath.EndsWith("/auth/metadata", StringComparison.Ordinal)
            ? Respond(HttpStatusCode.NotFound)
            : Respond(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var factoryCalls = 0;
        var options = new AzureDataExplorerExporterOptions
        {
            ClusterUri = ClusterUri,
            TokenCredential = new FakeCredential(),
            IngestionType = AzureDataExplorerIngestionType.Managed,
            HttpClientFactory = () =>
            {
                factoryCalls++;
                return httpClient;
            },
        };

        using (var ingestor = KustoRestIngestor.Create(options, Table, jsonMappingReference: null))
        {
            await IngestAsync(ingestor, Payload);
        }

        Assert.Equal(1, factoryCalls);
        Assert.Contains(handler.Requests, IsStreaming);

        // The exporter does not dispose a caller-supplied client.
        using var request = new HttpRequestMessage(HttpMethod.Get, ClusterUri);
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Create_ThrowsWhenHttpClientFactoryReturnsNull()
    {
        var options = new AzureDataExplorerExporterOptions
        {
            ClusterUri = ClusterUri,
            TokenCredential = new FakeCredential(),
            HttpClientFactory = () => null!,
        };

        Assert.Throws<InvalidOperationException>(() => KustoRestIngestor.Create(options, Table, jsonMappingReference: null));
    }

#if !NET
    [Fact]
    public async Task ConnectionLeaseHandler_SetsServicePointLeaseTimeout()
    {
        var uri = new Uri("https://lease-test.kusto.windows.net/v1/rest/ingest/db/table");
        using var inner = new FakeHandler(_ => Respond(HttpStatusCode.OK));
        using var httpClient = new HttpClient(new ConnectionLeaseHandler(inner), disposeHandler: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        using var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(
            (int)ConnectionLeaseHandler.ConnectionLeaseTimeout.TotalMilliseconds,
            ServicePointManager.FindServicePoint(uri).ConnectionLeaseTimeout);
    }
#endif

    private static KustoRestIngestor CreateIngestor(
        FakeHandler handler,
        AzureDataExplorerIngestionType ingestionType,
        string? mapping = null,
        bool authenticate = true,
        IKustoTokenProvider? tokenProvider = null)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope - the handler is owned by the test.
        var httpClient = new HttpClient(handler, disposeHandler: false);
#pragma warning restore CA2000 // Dispose objects before losing scope

        return new KustoRestIngestor(httpClient, authenticate ? tokenProvider ?? new StaticTokenProvider() : null, ClusterUri, Database, Table, mapping, ingestionType)
        {
            StreamingRetryDelay = TimeSpan.Zero,
        };
    }

    private static async Task IngestAsync(KustoRestIngestor ingestor, string payload)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        await ingestor.IngestAsync(stream, CancellationToken.None);
    }

    private static bool IsStreaming(RecordedRequest request) => request.Uri.AbsolutePath.StartsWith("/v1/rest/ingest/", StringComparison.Ordinal);

    private static bool IsQueueMessage(RecordedRequest request) => request.Uri.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal);

    private static int CountCommands(FakeHandler handler) => handler.Requests.Count(r => r.Uri.AbsolutePath == "/v1/rest/mgmt");

    private static HttpResponseMessage RespondToQueuedFlow(RecordedRequest request)
    {
        if (request.Uri.AbsolutePath == "/v1/rest/mgmt")
        {
            return GetCommand(request) switch
            {
                ".get ingestion resources" => Respond(HttpStatusCode.OK, IngestionResourcesResponse),
                ".get kusto identity token" => Respond(HttpStatusCode.OK, IdentityTokenResponse),
                _ => Respond(HttpStatusCode.BadRequest),
            };
        }

        return request.Method == HttpMethod.Put
            ? Respond(HttpStatusCode.Created)
            : IsQueueMessage(request) ? Respond(HttpStatusCode.Created) : Respond(HttpStatusCode.NotFound);
    }

    private static string? GetCommand(RecordedRequest request)
    {
        using var document = JsonDocument.Parse(request.Body);
        Assert.Equal("NetDefaultDB", document.RootElement.GetProperty("db").GetString());
        return document.RootElement.GetProperty("csl").GetString();
    }

    private static JsonElement GetQueuedMessage(RecordedRequest request)
    {
        var xml = Encoding.UTF8.GetString(request.Body);
        const string start = "<QueueMessage><MessageText>";
        const string end = "</MessageText></QueueMessage>";
        Assert.StartsWith(start, xml, StringComparison.Ordinal);
        Assert.EndsWith(end, xml, StringComparison.Ordinal);

        var json = Convert.FromBase64String(xml.Substring(start.Length, xml.Length - start.Length - end.Length));
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static HttpResponseMessage Respond(HttpStatusCode statusCode, string? body = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (body != null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return response;
    }

    private static string Decompress(byte[] data)
    {
        using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class RecordedRequest
    {
        public RecordedRequest(HttpMethod method, Uri uri, Dictionary<string, string> headers, byte[] body)
        {
            this.Method = method;
            this.Uri = uri;
            this.Headers = headers;
            this.Body = body;
        }

        public HttpMethod Method { get; }

        public Uri Uri { get; }

        public Dictionary<string, string> Headers { get; }

        public byte[] Body { get; }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<RecordedRequest, HttpResponseMessage> responder;
        private readonly List<RecordedRequest> requests = [];

        public FakeHandler(Func<RecordedRequest, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        public IReadOnlyList<RecordedRequest> Requests
        {
            get
            {
                lock (this.requests)
                {
                    return [.. this.requests];
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            var body = Array.Empty<byte>();
            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = string.Join(",", header.Value);
                }

#if NET
                body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
#else
                body = await request.Content.ReadAsByteArrayAsync();
#endif
            }

            var recorded = new RecordedRequest(request.Method, request.RequestUri!, headers, body);
            lock (this.requests)
            {
                this.requests.Add(recorded);
            }

            return this.responder(recorded);
        }
    }

    private sealed class StaticTokenProvider : IKustoTokenProvider
    {
        private int version = 1;

        public bool RotateOnInvalidate { get; set; }

        public List<string> InvalidatedTokens { get; } = [];

        public Task<string?> GetTokenAsync(Uri endpoint, CancellationToken cancellationToken)
            => Task.FromResult<string?>(this.version == 1 ? "test-token" : $"test-token-{this.version}");

        public void InvalidateToken(string token)
        {
            this.InvalidatedTokens.Add(token);
            if (this.RotateOnInvalidate)
            {
                this.version++;
            }
        }
    }

    private sealed class FakeCredential : TokenCredential
    {
        private int count;

        public List<string> Scopes { get; } = [];

        public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(1);

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            this.Scopes.AddRange(requestContext.Scopes);
            return new AccessToken($"token-{Interlocked.Increment(ref this.count)}", DateTimeOffset.UtcNow + this.Lifetime);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(this.GetToken(requestContext, cancellationToken));
    }
}
