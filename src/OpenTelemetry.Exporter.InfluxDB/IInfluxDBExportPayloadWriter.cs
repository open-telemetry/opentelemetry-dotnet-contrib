// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.InfluxDB;

internal interface IInfluxDBExportPayloadWriter
{
    Task WriteAsync(IReadOnlyCollection<string> lineProtocol, CancellationToken cancellationToken);
}
