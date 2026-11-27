// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

internal sealed class KustoAzureAdMetadata
{
    [JsonPropertyName("LoginEndpoint")]
    public string? LoginEndpoint { get; set; }

    [JsonPropertyName("LoginMfaRequired")]
    public bool LoginMfaRequired { get; set; }

    [JsonPropertyName("KustoClientAppId")]
    public string? KustoClientAppId { get; set; }

    [JsonPropertyName("KustoServiceResourceId")]
    public string? KustoServiceResourceId { get; set; }
}
