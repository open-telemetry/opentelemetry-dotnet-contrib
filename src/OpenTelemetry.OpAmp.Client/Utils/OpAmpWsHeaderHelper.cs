// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Utils;

internal class OpAmpWsHeaderHelper
{
    public const int OpAmpProtocolHeader = 0x00;
    public const int MaxHeaderLength = 10; // Maximum length for varint64 encoding

    public static int WriteHeader(ArraySegment<byte> buffer, ulong header = 0)
    {
        if (buffer.Count < MaxHeaderLength)
        {
            throw new InvalidOperationException("Ensure 10 bytes for buffer.");
        }

        return EncodeVarint64(buffer, header);
    }

    public static bool TryVerifyHeader(ReadOnlyMemory<byte> buffer, out int headerSize)
    {
        try
        {
            var header = DecodeVarint64(buffer.Span, out headerSize);
            return header == OpAmpProtocolHeader;
        }
        catch (Exception)
        {
            headerSize = -1;
            return false;
        }
    }

    public static ulong DecodeVarint64(ReadOnlySpan<byte> buffer, out int bytesRead)
    {
        ulong result = 0;
        int shift = 0;
        bytesRead = 0;

        foreach (byte b in buffer)
        {
            ulong value = (ulong)(b & 0x7F);
            result |= value << shift;
            bytesRead++;

            if ((b & 0x80) == 0)
            {
                return result;
            }

            shift += 7;

            // 64 bits max + buffer
            if (shift >= 70)
            {
                throw new OverflowException("Varint is too long for 64-bit integer.");
            }
        }

        throw new ArgumentException("Incomplete varint data.");
    }

    public static int EncodeVarint64(ArraySegment<byte> buffer, ulong value)
    {
        int bytesWritten = 0;

        while (value > 0x7F)
        {
            if (bytesWritten >= buffer.Count)
            {
                throw new ArgumentException("Buffer is too small for varint encoding.");
            }

            buffer[bytesWritten++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }

        if (bytesWritten >= buffer.Count)
        {
            throw new ArgumentException("Buffer is too small for varint encoding.");
        }

        buffer[bytesWritten++] = (byte)value; // Last byte without continuation bit

        return bytesWritten;
    }
}
