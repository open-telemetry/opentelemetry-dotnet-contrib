// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Utils;

internal static class OpAmpWsHeaderHelper
{
    public const int OpAmpProtocolHeader = 0x00;
    public const int MaxHeaderLength = 10; // Maximum length for varint64 encoding

    public static int WriteHeader(ArraySegment<byte> buffer, ulong header = 0)
    {
        if (buffer.Count < MaxHeaderLength)
        {
            throw new InvalidOperationException("Ensure 10 bytes for buffer.");
        }

        return Varint64.Encode(buffer, header);
    }

    public static bool TryVerifyHeader(ReadOnlyMemory<byte> buffer, out int headerSize)
    {
        try
        {
            var header = Varint64.Decode(buffer.Span, out headerSize);
            return header == OpAmpProtocolHeader;
        }
        catch (Exception)
        {
            headerSize = -1;
            return false;
        }
    }
}
