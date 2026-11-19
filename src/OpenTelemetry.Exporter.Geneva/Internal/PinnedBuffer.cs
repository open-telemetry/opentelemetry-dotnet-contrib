// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// Helpers for buffers whose address is handed to native code.
/// </summary>
/// <remarks>
/// On .NET the buffers come from the pinned object heap, so their address is stable for their whole lifetime and
/// no per-call <see cref="GCHandle"/> is needed (<c>GCHandle.Free</c> requires an unsafe context under the updated
/// memory safety rules). On .NET Framework / .NET Standard there is no pinned object heap, so callers
/// must fall back to pinning with a <see cref="GCHandle"/> around each native call.
/// </remarks>
internal static class PinnedBuffer
{
    public static byte[] Allocate(int length)
#if NET
        => GC.AllocateArray<byte>(length, pinned: true);
#else
        => new byte[length];
#endif

#if NET
    public static nint AddressOf(byte[] pinnedArray)
        => Marshal.UnsafeAddrOfPinnedArrayElement(pinnedArray, 0);
#endif
}
