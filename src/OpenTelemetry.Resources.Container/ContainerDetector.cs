// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace OpenTelemetry.Resources.Container;

/// <summary>
/// Resource detector for application running in Container environment.
/// </summary>
internal sealed partial class ContainerDetector : IResourceDetector
{
    private const string Filepath = "/proc/self/cgroup";
    private const string FilepathV2 = "/proc/self/mountinfo";
    private const string CrioMarker = "/crio-";
    private const string ContainerdMarker = "cri-containerd:";
    private const string ContainersMarker = "containers/";
    private const string CgroupFileSystemMarker = " - cgroup";
    private const string SandboxesMarker = "sandboxes/";

    // Matches the start of a line of /proc/self/mountinfo, up to and including the start of the "root" field.
    private const string MountInfoRootPrefix = @"^\d+ \d+ \d+:\d+ [^ ]*";

    // Matches a full-length container id that is not part of a longer run of hex characters, and captures it.
    private const string ContainerIdPattern = "((?<![0-9a-f])[0-9a-f]{64}(?![0-9a-f]))";

    private const string CrioPattern = MountInfoRootPrefix + "/crio-" + ContainerIdPattern;
    private const string ContainerdPattern = MountInfoRootPrefix + "cri-containerd:" + ContainerIdPattern;
    private const string ContainersDirectoryPattern = MountInfoRootPrefix + "/(?:overlay-)?containers/" + ContainerIdPattern + "/";
    private const string SandboxDirectoryPattern = MountInfoRootPrefix + "/sandboxes/" + ContainerIdPattern + "/";
    private const string DockerEnvFilePath = "/.dockerenv";
    private const string PodmanEnvFilePath = "/run/.containerenv";

    private static readonly Version SemanticConventionsVersion = new(1, 45, 0);

    /// <summary>
    /// CGroup Parse Versions.
    /// </summary>
    internal enum ParseMode
    {
        /// <summary>
        /// Represents CGroupV1.
        /// </summary>
        V1,

        /// <summary>
        /// Represents CGroupV2.
        /// </summary>
        V2,
    }

    /// <summary>
    /// Detects the resource attributes from Container.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var containerId =
            this.ExtractContainerId(Filepath, ParseMode.V1) ??
            this.ExtractContainerId(FilepathV2, ParseMode.V2);

        return BuildResource(containerId, DetectContainerRuntimeName());
    }

    /// <summary>
    /// Detects the container runtime from well-known marker files created by the runtime itself.
    /// </summary>
    /// <param name="dockerEnvPath">Path to the Docker marker file. Overridable for testing.</param>
    /// <param name="podmanEnvPath">Path to the Podman marker file. Overridable for testing.</param>
    /// <returns>The detected runtime name, or <see langword="null"/> if no known marker file is present.</returns>
    internal static string? DetectContainerRuntimeName(string dockerEnvPath = DockerEnvFilePath, string podmanEnvPath = PodmanEnvFilePath)
        => File.Exists(dockerEnvPath) ? "docker" : File.Exists(podmanEnvPath) ? "podman" : null;

    /// <summary>
    /// Builds the resource attributes from Container Id in file path.
    /// </summary>
    /// <param name="path">File path where container id exists.</param>
    /// <param name="cgroupVersion">CGroup Version of file to parse from.</param>
    /// <param name="dockerEnvPath">Path to the Docker marker file. Overridable for testing.</param>
    /// <param name="podmanEnvPath">Path to the Podman marker file. Overridable for testing.</param>
    /// <returns>Returns Resource with list of key-value pairs of container resource attributes if a container id or runtime is detected, else empty resource.</returns>
    internal Resource BuildResource(string path, ParseMode cgroupVersion, string dockerEnvPath = DockerEnvFilePath, string podmanEnvPath = PodmanEnvFilePath)
    {
        var containerId = this.ExtractContainerId(path, cgroupVersion);
        var runtimeName = DetectContainerRuntimeName(dockerEnvPath, podmanEnvPath);

        return BuildResource(containerId, runtimeName);
    }

    /// <summary>
    /// Builds the resource attributes from an already-detected container id and/or runtime name.
    /// </summary>
    /// <param name="containerId">The detected container id, or <see langword="null"/> if not detected.</param>
    /// <param name="runtimeName">The detected container runtime name, or <see langword="null"/> if not detected.</param>
    /// <returns>Returns Resource with list of key-value pairs of container resource attributes if a container id or runtime is detected, else empty resource.</returns>
    private static Resource BuildResource(string? containerId, string? runtimeName)
    {
        var hasContainerId = containerId is { Length: > 0 };
        if (!hasContainerId && runtimeName is null)
        {
            return Resource.Empty;
        }

        var attributeList = new List<KeyValuePair<string, object>>(2);

        if (hasContainerId)
        {
            attributeList.Add(new(ContainerSemanticConventions.AttributeContainerId, containerId!));
        }

        if (runtimeName is not null)
        {
            attributeList.Add(new(ContainerSemanticConventions.AttributeContainerRuntimeName, runtimeName));
        }

        return new Resource(attributeList, Internal.SchemaUrls.Get(SemanticConventionsVersion));
    }

    /// <summary>
    /// Gets the Container Id from the lines of a cgroup v2 <c>/proc/self/mountinfo</c> file.
    /// </summary>
    /// <remarks>
    /// The following sources are consulted, in order of preference:
    /// <list type="number">
    /// <item><description>A CRI-O cgroup path (<c>/crio-&lt;id&gt;</c>) in the root of a cgroup mount.</description></item>
    /// <item><description>A containerd CRI cgroup path (<c>cri-containerd:&lt;id&gt;</c>) in the root of a cgroup mount.</description></item>
    /// <item><description>The per-container directory of the container runtime (<c>/containers/&lt;id&gt;/</c> or <c>/overlay-containers/&lt;id&gt;/</c>) in the root of any mount, such as the source of the <c>/etc/hostname</c> bind mount.</description></item>
    /// </list>
    /// Paths such as <c>.../sandboxes/&lt;id&gt;/hostname</c> are deliberately ignored: when running in Kubernetes with containerd
    /// that is the id of the pod sandbox, which is shared by every container in the pod and is not the id of the container itself.
    /// </remarks>
    /// <param name="lines">The lines read from the mountinfo file.</param>
    /// <returns>Container Id, Null if not found.</returns>
    private static string? GetContainerIdV2(IEnumerable<string> lines)
    {
        string? containerdId = null;
        string? sandboxId = null;
        List<string>? containersIds = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            // The CRI-O and containerd ids are only trusted when they are in the root of a cgroup mount.
            var isCgroupMount = ContainsOrdinal(line, CgroupFileSystemMarker);

            if (isCgroupMount && ContainsOrdinal(line, CrioMarker))
            {
                var match = CrioContainerIdRegex().Match(line);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            if (isCgroupMount && containerdId is null && ContainsOrdinal(line, ContainerdMarker))
            {
                var match = ContainerdContainerIdRegex().Match(line);
                if (match.Success)
                {
                    containerdId = match.Groups[1].Value;
                }
            }

            if (sandboxId is null && ContainsOrdinal(line, SandboxesMarker))
            {
                var match = SandboxDirectoryIdRegex().Match(line);
                if (match.Success)
                {
                    sandboxId = match.Groups[1].Value;
                }
            }

            if (ContainsOrdinal(line, ContainersMarker))
            {
                var match = ContainersDirectoryContainerIdRegex().Match(line);
                if (match.Success)
                {
                    var id = match.Groups[1].Value;
                    containersIds ??= [];
                    if (!containersIds.Contains(id))
                    {
                        containersIds.Add(id);
                    }
                }
            }
        }

        if (containerdId is not null)
        {
            return containerdId;
        }

        if (containersIds is null)
        {
            if (sandboxId is not null)
            {
                ContainerExtensionsEventSource.Log.SandboxContainerIdIgnored(sandboxId);
            }

            return null;
        }

        if (containersIds.Count > 1)
        {
            ContainerExtensionsEventSource.Log.MultipleContainerIdsFound(containersIds);
        }

        // Consistent with the OpenTelemetry Java implementation, prefer the last of the distinct ids (in order of first appearance).
        return containersIds[containersIds.Count - 1];
    }

    private static bool ContainsOrdinal(string value, string text)
#if NET
        => value.Contains(text, StringComparison.Ordinal);
#else
        => value.IndexOf(text, StringComparison.Ordinal) >= 0;
#endif

#if NET
    [GeneratedRegex(CrioPattern)]
    private static partial Regex CrioContainerIdRegex();

    [GeneratedRegex(ContainerdPattern)]
    private static partial Regex ContainerdContainerIdRegex();

    [GeneratedRegex(ContainersDirectoryPattern)]
    private static partial Regex ContainersDirectoryContainerIdRegex();

    [GeneratedRegex(SandboxDirectoryPattern)]
    private static partial Regex SandboxDirectoryIdRegex();
#else
#pragma warning disable SA1201 // A field should not follow a method
    private static readonly Regex CrioContainerIdRegexField = new(CrioPattern);
    private static readonly Regex ContainerdContainerIdRegexField = new(ContainerdPattern);
    private static readonly Regex ContainersDirectoryContainerIdRegexField = new(ContainersDirectoryPattern);
    private static readonly Regex SandboxDirectoryIdRegexField = new(SandboxDirectoryPattern);
#pragma warning restore SA1201 // A field should not follow a method

    private static Regex CrioContainerIdRegex() => CrioContainerIdRegexField;

    private static Regex ContainerdContainerIdRegex() => ContainerdContainerIdRegexField;

    private static Regex ContainersDirectoryContainerIdRegex() => ContainersDirectoryContainerIdRegexField;

    private static Regex SandboxDirectoryIdRegex() => SandboxDirectoryIdRegexField;
#endif

    /// <summary>
    /// Extracts Container Id from path using the cgroupv1 or cgroupv2 format.
    /// </summary>
    /// <param name="path">cgroup path.</param>
    /// <param name="cgroupVersion">CGroup Version of file to parse from.</param>
    /// <returns>Container Id, Null if not found or exception being thrown.</returns>
    private string? ExtractContainerId(string path, ParseMode cgroupVersion)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            if (cgroupVersion == ParseMode.V2)
            {
                return GetContainerIdV2(File.ReadLines(path));
            }

            foreach (var line in File.ReadLines(path))
            {
                if (!string.IsNullOrEmpty(line))
                {
                    var containerId = CgroupContainerIdParser.GetContainerId(line);
                    if (!string.IsNullOrEmpty(containerId))
                    {
                        return containerId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ContainerExtensionsEventSource.Log.ExtractResourceAttributesException($"{nameof(ContainerDetector)} : Failed to extract Container id from path", ex);
        }

        return null;
    }
}
