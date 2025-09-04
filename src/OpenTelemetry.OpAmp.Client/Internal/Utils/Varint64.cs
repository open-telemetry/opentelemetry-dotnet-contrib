// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Utils;

internal static class Varint64
{
    private const byte DataMask = 0x7F;                         // 0111 1111 - lower 7 bits of a byte
    private const byte ContinuationBit = 0x80;                  // 1000 0000 - signals more bytes follow
    private const int BitsPerByte = 7;                          // Varint encodes 7 bits per byte
    private const int MaxEncodedBytes = 10;                     // 64 bits / 7 bits per byte = max 10 bytes
    private const int MaxShift = BitsPerByte * MaxEncodedBytes; // 70 bits (safety limit)

    public static ulong Decode(ReadOnlySpan<byte> buffer, out int bytesRead)
    {
        ulong result = 0;
        int shift = 0;
        bytesRead = 0;

        foreach (byte b in buffer)
        {
            ulong value = (ulong)(b & DataMask);
            result |= value << shift;
            bytesRead++;

            if ((b & ContinuationBit) == 0)
            {
                return result;
            }

            shift += BitsPerByte;

            // 64 bits max + buffer
            if (shift >= MaxShift)
            {
                throw new OverflowException("Varint is too long for 64-bit integer.");
            }
        }

        throw new ArgumentException("Incomplete varint data.");
    }

    public static int Encode(ArraySegment<byte> buffer, ulong value)
    {
        int bytesWritten = 0;
        int offset = buffer.Offset;

        while (value > DataMask)
        {
            if (bytesWritten >= buffer.Count)
            {
                throw new ArgumentException("Buffer is too small for varint encoding.");
            }

            buffer.Array![offset + bytesWritten++] = (byte)((value & DataMask) | ContinuationBit);
            value >>= BitsPerByte;
        }

        if (bytesWritten >= buffer.Count)
        {
            throw new ArgumentException("Buffer is too small for varint encoding.");
        }

        buffer.Array![offset + bytesWritten++] = (byte)value; // Last byte without continuation bit

        return bytesWritten;
    }
}
