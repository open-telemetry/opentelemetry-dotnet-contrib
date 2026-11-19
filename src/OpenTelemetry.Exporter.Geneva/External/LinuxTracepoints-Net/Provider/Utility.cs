#nullable enable

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.LinuxTracepoints.Provider;

using System;
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
        if (!value.TryWriteBytes(destination, bigEndian: true, out _))
        {
            throw new ArgumentOutOfRangeException(nameof(destination));
        }
    }

    /// <summary>
    /// Writes a 16-bit value in native byte order.
    /// </summary>
    public static void WriteUInt16Native(Span<byte> destination, ushort value)
    {
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        }
    }

    /// <summary>
    /// Writes a 32-bit value in native byte order.
    /// </summary>
    public static void WriteInt32Native(Span<byte> destination, int value)
    {
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteInt32BigEndian(destination, value);
        }
    }
}
