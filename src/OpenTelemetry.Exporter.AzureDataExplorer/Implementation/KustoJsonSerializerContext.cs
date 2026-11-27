// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Source generated serialization metadata for the Kusto REST payloads used by
/// <see cref="KustoRestIngestor"/>, keeping the exporter trimming and native AOT safe.
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(KustoAuthMetadataResponse))]
[JsonSerializable(typeof(KustoManagementRequest))]
[JsonSerializable(typeof(KustoV1Response))]
[JsonSerializable(typeof(KustoErrorResponse))]
[JsonSerializable(typeof(KustoQueuedIngestionMessage))]
internal sealed partial class KustoJsonSerializerContext : JsonSerializerContext
{
}
