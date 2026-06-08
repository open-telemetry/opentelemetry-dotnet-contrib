// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using InfluxDB.Client;

namespace OpenTelemetry.Exporter.InfluxDB;

internal sealed class InfluxDBExportPayloadWriter : IInfluxDBExportPayloadWriter
{
    private readonly IWriteApiAsync writeApiAsync;

    public InfluxDBExportPayloadWriter(IWriteApiAsync writeApiAsync)
    {
        this.writeApiAsync = writeApiAsync;
    }

    public Task WriteAsync(IReadOnlyCollection<string> lineProtocol, CancellationToken cancellationToken) =>
        this.writeApiAsync.WriteRecordsAsync(lineProtocol.ToArray(), cancellationToken: cancellationToken);
}
