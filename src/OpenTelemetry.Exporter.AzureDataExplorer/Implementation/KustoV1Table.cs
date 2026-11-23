// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

internal sealed class KustoV1Table
{
    [JsonPropertyName("TableName")]
    public string? TableName { get; set; }

    [JsonPropertyName("Columns")]
    public List<KustoV1Column>? Columns { get; set; }

    [JsonPropertyName("Rows")]
    public List<List<JsonElement>>? Rows { get; set; }
}
