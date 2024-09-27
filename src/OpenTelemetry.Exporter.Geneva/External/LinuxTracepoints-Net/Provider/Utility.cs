// Source: https://github.com/microsoft/LinuxTracepoints-Net/blob/974c47522d053c915009ef5112840026eaf22adb/Provider/Utility.cs

#if NET6_0_OR_GREATER

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.LinuxTracepoints.Provider;

using System;
using System.Runtime.InteropServices;
using Interlocked = System.Threading.Interlocked;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

internal static class Utility
{
    /// <summary>
    /// Atomically: old = location; if (old != null) { return old; } else { location = value; return value; }
    /// </summary>
    public static T InterlockedInitSingleton<T>(ref T? location, T value)
        where T : class
    {
        return Interlocked.CompareExchange(ref location, value, null) ?? value;
    }

    public static void WriteGuidBigEndian(Span<byte> destination, Guid value)
    {
        if (BitConverter.IsLittleEndian)
        {
            unsafe
            {
                var p = (byte*)&value;
                var p0 = (uint*)p;
                var p1 = (ushort*)(p + 4);
                var p2 = (ushort*)(p + 6);
                *p0 = BinaryPrimitives.ReverseEndianness(*p0);
                *p1 = BinaryPrimitives.ReverseEndianness(*p1);
                *p2 = BinaryPrimitives.ReverseEndianness(*p2);
            }
        }

#if NET8_0_OR_GREATER
        MemoryMarshal.Write(destination, in value);
#else
        MemoryMarshal.Write(destination, ref value);
#endif
    }
}

#endif
