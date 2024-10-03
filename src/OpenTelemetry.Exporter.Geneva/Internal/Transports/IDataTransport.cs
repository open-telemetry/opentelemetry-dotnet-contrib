// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter.Geneva.Transports;

internal interface IDataTransport
{
    bool IsEnabled();

    void Send(byte[] data, int size);
}
