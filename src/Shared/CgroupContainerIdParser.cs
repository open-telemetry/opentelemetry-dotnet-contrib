// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources;

/// <summary>
/// Parses container ids from the lines of a cgroup v1 file (<c>/proc/self/cgroup</c>).
/// </summary>
internal static class CgroupContainerIdParser
{
    // The length of a full container id, as used by container runtimes such as Docker, containerd, CRI-O and Podman in cgroup paths.
    private const int ContainerIdLength = 64;

    /// <summary>
    /// Gets the container id from a line of a cgroup v1 file, such as <c>14:name=systemd:/docker/&lt;id&gt;</c>.
    /// </summary>
    /// <param name="line">The line read from the cgroup file.</param>
    /// <returns>The container id, or <see langword="null"/> if the line does not contain one.</returns>
    public static string? GetContainerId(string line)
    {
        var lastSlashIndex = line.LastIndexOf('/', StringComparison.Ordinal);
        if (lastSlashIndex < 0)
        {
            return null;
        }

        var lastSection = line.Substring(lastSlashIndex + 1);

        string containerId;
        var colonIndex = lastSection.LastIndexOf(':', StringComparison.Ordinal);

        if (colonIndex != -1)
        {
            // Since containerd v1.5.0, the container id is separated by the last colon when the systemd cgroup driver is used:
            // https://github.com/containerd/containerd/blob/release/1.5/pkg/cri/server/helpers_linux.go#L64
            containerId = lastSection.Substring(colonIndex + 1);
        }
        else
        {
            var startIndex = lastSection.LastIndexOf('-', StringComparison.Ordinal);
            var endIndex = lastSection.LastIndexOf('.', StringComparison.Ordinal);

            startIndex = (startIndex == -1) ? 0 : startIndex + 1;

            if (endIndex == -1)
            {
                endIndex = lastSection.Length;
            }

            if (startIndex > endIndex)
            {
                return null;
            }

            containerId = lastSection.Substring(startIndex, endIndex - startIndex);
        }

        // Only accept full-length container ids so that the names of cgroups that do not belong to
        // a container, such as systemd units (e.g. session-2.scope), are not mistaken for one.
        return containerId.Length == ContainerIdLength && IsHexString(containerId) ? containerId : null;
    }

    private static bool IsHexString(string value)
    {
        foreach (var ch in value)
        {
            if (!char.IsAsciiHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

#if !NET11_0_OR_GREATER
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int LastIndexOf(this string str, char value, StringComparison comparisonType)
    {
        System.Diagnostics.Debug.Assert(comparisonType == StringComparison.Ordinal, "Only StringComparison.Ordinal is supported.");
        return str.LastIndexOf(value);
    }
#endif
}
