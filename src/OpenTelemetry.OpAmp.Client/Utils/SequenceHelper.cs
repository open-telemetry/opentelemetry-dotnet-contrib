// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace OpenTelemetry.OpAmp.Client.Utils;

internal static class SequenceHelper
{
    public static ReadOnlySequence<byte> AsSequence(this byte[] message)
    {
        if (message == null || message.Length == 0)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        return new ReadOnlySequence<byte>(message);
    }
}
