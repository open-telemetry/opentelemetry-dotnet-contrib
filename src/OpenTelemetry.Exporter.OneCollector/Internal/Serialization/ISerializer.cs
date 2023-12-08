// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OneCollector;

internal interface ISerializer<T>
    where T : class
{
    string Description { get; }

    OneCollectorExporterSerializationFormatType SerializationFormat { get; }

    void SerializeBatchOfItemsToStream(
        Resource resource,
        in Batch<T> batch,
        Stream stream,
        int initialSizeOfPayloadInBytes,
        out BatchSerializationResult serializationResult);
}
