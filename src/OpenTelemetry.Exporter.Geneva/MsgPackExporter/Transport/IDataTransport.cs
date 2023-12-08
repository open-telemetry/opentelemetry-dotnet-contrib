// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Geneva;

internal interface IDataTransport
{
    bool IsEnabled();

    void Send(byte[] data, int size);
}
