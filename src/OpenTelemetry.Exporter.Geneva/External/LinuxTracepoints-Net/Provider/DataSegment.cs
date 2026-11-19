#nullable enable

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.LinuxTracepoints.Provider;

using System.Runtime.InteropServices;

/// <summary>
/// struct iovec, for use with calls to writev.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DataSegment
{
    public nint PinnedBase;
    public nuint Length;

    public DataSegment(nint pinnedBase, nuint length)
    {
        this.PinnedBase = pinnedBase;
        this.Length = length;
    }
}
