// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal record MockFrame
{
    public ReadOnlySequence<byte> Frame { get; set; }

    public bool HasHeader { get; set; }

    public required string ExptectedContent { get; set; }
}
