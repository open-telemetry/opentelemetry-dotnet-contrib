// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

internal sealed class KustoV1Column
{
    [JsonPropertyName("ColumnName")]
    public string? ColumnName { get; set; }
}
