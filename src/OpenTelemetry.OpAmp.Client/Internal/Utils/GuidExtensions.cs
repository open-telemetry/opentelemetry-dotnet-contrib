// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Utils;

internal static class GuidExtensions
{
    // RFC 9562 (big-endian) byte order. Guid.ToByteArray() writes the first
    // three fields little-endian.
    public static byte[] ToBigEndianByteArray(this Guid guid)
    {
#if NET8_0_OR_GREATER
        return guid.ToByteArray(bigEndian: true);
#else
        var bytes = guid.ToByteArray();
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
        return bytes;
#endif
    }
}
