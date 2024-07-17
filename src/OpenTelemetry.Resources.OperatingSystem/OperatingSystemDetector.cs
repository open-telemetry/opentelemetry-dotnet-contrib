// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Runtime.InteropServices;
#endif
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
#if NETFRAMEWORK
        return OperatingSystemsValues.Windows;
#else
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OperatingSystemsValues.Windows :
                   RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OperatingSystemsValues.Linux :
                   RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OperatingSystemsValues.Darwin :
                   null;
#endif
    }
}
