// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Azure.Core;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Acquires Microsoft Entra tokens for Kusto endpoints. The scope (and the
/// authority host used by application key authentication) is discovered from
/// the cluster's unauthenticated <c>/v1/rest/auth/metadata</c> endpoint, which
/// is how the Kusto SDK supports sovereign clouds and MFA-enabled clusters.
/// </summary>
internal sealed class KustoTokenProvider : IKustoTokenProvider
{
    internal const string DefaultKustoResourceId = "https://kusto.kusto.windows.net";
    internal const string DefaultLoginEndpoint = "https://login.microsoftonline.com";

    private const int MaxMetadataLength = 1024 * 1024;

    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly HttpClient httpClient;
    private readonly Func<Uri, TokenCredential> credentialFactory;
    private readonly object sync = new();
    private readonly Dictionary<string, Task<KustoAuthSettings>> authSettings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AccessToken> tokens = new(StringComparer.Ordinal);
    private TokenCredential? credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="KustoTokenProvider"/> class.
    /// </summary>
    /// <param name="httpClient">Client used to read the cluster auth metadata.</param>
    /// <param name="credentialFactory">Creates the credential given the authority host advertised by the cluster.</param>
    public KustoTokenProvider(HttpClient httpClient, Func<Uri, TokenCredential> credentialFactory)
    {
        this.httpClient = httpClient;
        this.credentialFactory = credentialFactory;
    }

    public async Task<string?> GetTokenAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var settings = await this.GetAuthSettingsAsync(endpoint, cancellationToken).ConfigureAwait(false);

        TokenCredential credential;
        lock (this.sync)
        {
            if (this.tokens.TryGetValue(settings.Scope, out var cached) && !NeedsRefresh(cached))
            {
                return cached.Token;
            }

            credential = this.credential ??= this.credentialFactory(settings.AuthorityHost);
        }

        var token = await credential
            .GetTokenAsync(new TokenRequestContext([settings.Scope]), cancellationToken)
            .ConfigureAwait(false);

        lock (this.sync)
        {
            this.tokens[settings.Scope] = token;
        }

        return token.Token;
    }

    public void InvalidateToken(string token)
    {
        lock (this.sync)
        {
            foreach (var entry in this.tokens)
            {
                if (string.Equals(entry.Value.Token, token, StringComparison.Ordinal))
                {
                    this.tokens.Remove(entry.Key);
                    break;
                }
            }
        }
    }

    internal static KustoAuthSettings ParseAuthSettings(KustoAzureAdMetadata? metadata)
    {
        var resource = string.IsNullOrWhiteSpace(metadata?.KustoServiceResourceId)
            ? DefaultKustoResourceId
            : metadata!.KustoServiceResourceId!.Trim().TrimEnd('/');

        if (metadata?.LoginMfaRequired == true)
        {
            // Mirrors the Kusto SDK: MFA-enabled clusters use the kustomfa resource.
#if NET
            resource = resource.Replace(".kusto.", ".kustomfa.", StringComparison.Ordinal);
#else
            resource = resource.Replace(".kusto.", ".kustomfa.");
#endif
        }

        var loginEndpoint = string.IsNullOrWhiteSpace(metadata?.LoginEndpoint)
            ? DefaultLoginEndpoint
            : metadata!.LoginEndpoint!.Trim();

        return new KustoAuthSettings(resource + "/.default", new Uri(loginEndpoint, UriKind.Absolute));
    }

    private static bool NeedsRefresh(AccessToken token)
    {
        var now = DateTimeOffset.UtcNow;
        return token.ExpiresOn - RefreshMargin <= now
            || (token.RefreshOn.HasValue && token.RefreshOn.Value <= now);
    }

    private Task<KustoAuthSettings> GetAuthSettingsAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var origin = endpoint.GetLeftPart(UriPartial.Authority);

        lock (this.sync)
        {
            if (!this.authSettings.TryGetValue(origin, out var task) || task.IsFaulted || task.IsCanceled)
            {
                task = this.FetchAuthSettingsAsync(new Uri(origin + "/v1/rest/auth/metadata"), cancellationToken);
                this.authSettings[origin] = task;
            }

            return task;
        }
    }

    private async Task<KustoAuthSettings> FetchAuthSettingsAsync(Uri metadataUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, metadataUri);
        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Older or emulated endpoints may not expose metadata; assume the public cloud.
            return ParseAuthSettings(null);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new AzureDataExplorerIngestionException(
                $"Failed to read authentication metadata from '{metadataUri.Host}' (HTTP {(int)response.StatusCode}).",
                isPermanent: false,
                response.StatusCode);
        }

        var body = await HttpClientHelpers.GetResponseBodyAsByteArrayAsync(MaxMetadataLength, response, cancellationToken).ConfigureAwait(false);
        KustoAuthMetadataResponse? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize(body, KustoJsonSerializerContext.Default.KustoAuthMetadataResponse);
        }
        catch (JsonException ex)
        {
            throw new AzureDataExplorerIngestionException(
                $"The authentication metadata from '{metadataUri.Host}' was not valid JSON.",
                isPermanent: false,
                response.StatusCode,
                innerException: ex);
        }

        return ParseAuthSettings(metadata?.AzureAD);
    }

    internal sealed class KustoAuthSettings
    {
        public KustoAuthSettings(string scope, Uri authorityHost)
        {
            this.Scope = scope;
            this.AuthorityHost = authorityHost;
        }

        public string Scope { get; }

        public Uri AuthorityHost { get; }
    }
}
