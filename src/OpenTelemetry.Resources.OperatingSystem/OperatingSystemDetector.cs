// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.OperatingSystem;

/// <summary>
/// Operating system detector.
/// </summary>
internal sealed class OperatingSystemDetector : IResourceDetector
{
    /// <summary>
    /// Detects the resource attributes from the operating system.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var osType = GetOSType();

        if (osType == null)
        {
            return Resource.Empty;
        }

        return new Resource(
        [
            new(OperatingSystemSemanticConventions.AttributeOperatingSystemType, osType),
        ]);
    }

    private static string? GetOSType()
    {
        var platform = Environment.OSVersion.Platform;
        if (platform == PlatformID.Win32NT)
        {
            return "windows";
        }

        if (platform == PlatformID.MacOSX)
        {
            return "darwin";
        }

        if (platform == PlatformID.Unix)
        {
            return "linux";
        }

        return null;
    }
}
