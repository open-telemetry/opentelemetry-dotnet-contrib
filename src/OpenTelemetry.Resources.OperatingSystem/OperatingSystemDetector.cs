// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using static OpenTelemetry.Resources.OperatingSystem.OperatingSystemSemanticConventions;

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
            new(AttributeOperatingSystemType, osType),
        ]);
    }

    private static string? GetOSType()
    {
        var platform = Environment.OSVersion.Platform;
        if (platform == PlatformID.Win32NT)
        {
            return OperatingSystemsValues.Windows;
        }

        if (platform == PlatformID.MacOSX)
        {
            return OperatingSystemsValues.Darwin;
        }

        if (platform == PlatformID.Unix)
        {
            return OperatingSystemsValues.Linux;
        }

        return null;
    }
}
