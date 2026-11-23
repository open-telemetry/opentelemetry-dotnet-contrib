// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Provides bearer tokens for Kusto REST requests.
/// </summary>
internal interface IKustoTokenProvider
{
    /// <summary>
    /// Gets a bearer token for requests to <paramref name="endpoint"/>, or
    /// <see langword="null"/> when requests should be sent unauthenticated.
    /// </summary>
    /// <param name="endpoint">The Kusto endpoint the request is sent to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token.</returns>
    Task<string?> GetTokenAsync(Uri endpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Discards <paramref name="token"/> if it is still cached so the next call to
    /// <see cref="GetTokenAsync"/> acquires a new token from the credential.
    /// </summary>
    /// <param name="token">The token that was rejected.</param>
    void InvalidateToken(string token);
}
