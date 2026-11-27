// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// The v1 response format returned by <c>/v1/rest/mgmt</c>.
/// </summary>
internal sealed class KustoV1Response
{
    [JsonPropertyName("Tables")]
    public List<KustoV1Table>? Tables { get; set; }
}
