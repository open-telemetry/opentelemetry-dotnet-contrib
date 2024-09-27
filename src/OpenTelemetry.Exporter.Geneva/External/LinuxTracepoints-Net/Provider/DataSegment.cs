// Source: https://github.com/microsoft/LinuxTracepoints-Net/blob/974c47522d053c915009ef5112840026eaf22adb/Provider/DataSegment.cs

#if NET6_0_OR_GREATER

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

namespace Microsoft.LinuxTracepoints.Provider;

using System.Runtime.InteropServices;

/// <summary>
/// struct iovec, for use with calls to writev.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DataSegment
{
    public void* PinnedBase;
    public nuint Length;

    public DataSegment(void* pinnedBase, nuint length)
    {
        this.PinnedBase = pinnedBase;
        this.Length = length;
    }
}

#endif
