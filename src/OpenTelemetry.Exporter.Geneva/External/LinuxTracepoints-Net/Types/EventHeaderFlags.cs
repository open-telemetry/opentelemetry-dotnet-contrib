#if NET6_0_OR_GREATER

// Source: https://github.com/microsoft/LinuxTracepoints-Net/blob/974c47522d053c915009ef5112840026eaf22adb/Types/EventHeaderFlags.cs

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

namespace Microsoft.LinuxTracepoints
{
    /// <summary>
    /// Values for EventHeader.Flags.
    /// </summary>
    [System.Flags]
    internal enum EventHeaderFlags : byte
    {
        /// <summary>
        /// Pointer32, BigEndian, no extensions.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Pointer is 64 bits, not 32 bits.
        /// </summary>
        Pointer64 = 0x01,

        /// <summary>
        /// Event uses little-endian, not big-endian.
        /// </summary>
        LittleEndian = 0x02,

        /// <summary>
        /// There is at least one EventHeaderExtension block.
        /// </summary>
        Extension = 0x04,
    }
}

#endif
