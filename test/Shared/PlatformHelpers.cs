// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type

using System.Runtime.InteropServices;
using Xunit;

namespace OpenTelemetry.Tests;

internal enum TestPlatform
{
    Unknown = 0,
    Windows = 1,
    Linux = 2,
    OSX = 3,
}

internal sealed class TestPlatformHelpers
{
    public static bool IsProcessElevated(TestPlatform platform)
    {
        if (platform != TestPlatform.Linux)
        {
            // TODO: Add support for this check on other platforms as needed.
            throw new NotImplementedException();
        }

        uint userId = SystemNativeUnix.GetEUid();
        return userId == 0;
    }

    // From: https://github.com/dotnet/corefx/blob/v2.2.8/src/Common/src/Interop/Unix/System.Native/Interop.GetEUid.cs
    private static class SystemNativeUnix
    {
#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
        [DllImport("libc", SetLastError = true)]
        internal static extern uint GetEUid();
#pragma warning restore CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
    }
}

internal sealed class SkipUnlessPlatformMatchesFactAttribute : FactAttribute
{
    public SkipUnlessPlatformMatchesFactAttribute(TestPlatform platform, bool requireElevatedProcess = false)
    {
        var osPlatform = platform switch
        {
            TestPlatform.Windows => OSPlatform.Windows,
            TestPlatform.Linux => OSPlatform.Linux,
            TestPlatform.OSX => OSPlatform.OSX,
            _ => throw new NotSupportedException($"TestPlatform '{platform}' is not supported"),
        };

        if (!RuntimeInformation.IsOSPlatform(osPlatform))
        {
            this.Skip = $"Skipped because current platform does not match requested '{platform}' platform.";
            return;
        }

        if (requireElevatedProcess
            && !TestPlatformHelpers.IsProcessElevated(platform))
        {
            this.Skip = $"Skipped because current process isn't elevated.";
            return;
        }
    }
}

internal sealed class SkipUnlessPlatformMatchesTheoryAttribute : TheoryAttribute
{
    public SkipUnlessPlatformMatchesTheoryAttribute(TestPlatform platform, bool requireElevatedProcess = false)
    {
        var osPlatform = platform switch
        {
            TestPlatform.Windows => OSPlatform.Windows,
            TestPlatform.Linux => OSPlatform.Linux,
            TestPlatform.OSX => OSPlatform.OSX,
            _ => throw new NotSupportedException($"TestPlatform '{platform}' is not supported"),
        };

        if (!RuntimeInformation.IsOSPlatform(osPlatform))
        {
            this.Skip = $"Skipped because current platform does not match requested '{platform}' platform.";
            return;
        }

        if (requireElevatedProcess
            && !TestPlatformHelpers.IsProcessElevated(platform))
        {
            this.Skip = $"Skipped because current process isn't elevated.";
            return;
        }
    }
}
