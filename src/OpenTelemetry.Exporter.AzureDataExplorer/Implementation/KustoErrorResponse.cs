// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// The error body returned by the Kusto REST API for failed requests.
/// </summary>
internal sealed class KustoErrorResponse
{
    [JsonPropertyName("error")]
    public KustoError? Error { get; set; }
}
