// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Response of the unauthenticated <c>/v1/rest/auth/metadata</c> endpoint.
/// </summary>
internal sealed class KustoAuthMetadataResponse
{
    [JsonPropertyName("AzureAD")]
    public KustoAzureAdMetadata? AzureAD { get; set; }
}
