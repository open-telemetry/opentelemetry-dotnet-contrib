// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Utils;

// Converts between Guid and RFC 9562 (big-endian) byte order. Guid.ToByteArray()
// and new Guid(byte[]) use the first three fields little-endian.
internal static class GuidExtensions
{
    public static byte[] ToBigEndianByteArray(this Guid guid)
    {
#if NET8_0_OR_GREATER
        return guid.ToByteArray(bigEndian: true);
#else
        var bytes = guid.ToByteArray();
        SwapFieldByteOrder(bytes);
        return bytes;
#endif
    }

    public static Guid FromBigEndianBytes(ReadOnlySpan<byte> bytes)
    {
#if NET8_0_OR_GREATER
        return new Guid(bytes, bigEndian: true);
#else
        var copy = bytes.ToArray();
        SwapFieldByteOrder(copy);
        return new Guid(copy);
#endif
    }

#if !NET8_0_OR_GREATER
    private static void SwapFieldByteOrder(byte[] bytes)
    {
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
    }
#endif
}
