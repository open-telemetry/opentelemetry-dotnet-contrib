// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Body of a <c>/v1/rest/mgmt</c> request.
/// </summary>
internal sealed class KustoManagementRequest
{
    [JsonPropertyName("db")]
    public string? Database { get; set; }

    [JsonPropertyName("csl")]
    public string? Command { get; set; }
}
