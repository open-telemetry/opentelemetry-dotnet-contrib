// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class ContainerAttributes
{
    /// <summary>
    /// The command used to run the container (i.e. the command name).
    /// </summary>
    /// <remarks>
    /// If using embedded credentials or sensitive data, it is recommended to remove them to prevent potential leakage.
    /// </remarks>
    public const string AttributeContainerCommand = "container.command";

    /// <summary>
    /// All the command arguments (including the command/executable itself) run by the container.
    /// </summary>
    public const string AttributeContainerCommandArgs = "container.command_args";

    /// <summary>
    /// The full command run by the container as a single string representing the full command.
    /// </summary>
    public const string AttributeContainerCommandLine = "container.command_line";

    /// <summary>
    /// Deprecated, use <c>cpu.mode</c> instead.
    /// </summary>
    [Obsolete("Replaced by <c>cpu.mode</c>.")]
    public const string AttributeContainerCpuState = "container.cpu.state";

    /// <summary>
    /// The name of the CSI (<a href="https://github.com/container-storage-interface/spec">Container Storage Interface</a>) plugin used by the volume.
    /// </summary>
    /// <remarks>
    /// This can sometimes be referred to as a "driver" in CSI implementations. This should represent the <c>name</c> field of the GetPluginInfo RPC.
    /// </remarks>
    public const string AttributeContainerCsiPluginName = "container.csi.plugin.name";

    /// <summary>
    /// The unique volume ID returned by the CSI (<a href="https://github.com/container-storage-interface/spec">Container Storage Interface</a>) plugin.
    /// </summary>
    /// <remarks>
    /// This can sometimes be referred to as a "volume handle" in CSI implementations. This should represent the <c>Volume.volume_id</c> field in CSI spec.
    /// </remarks>
    public const string AttributeContainerCsiVolumeId = "container.csi.volume.id";

    /// <summary>
    /// Container ID. Usually a UUID, as for example used to <a href="https://docs.docker.com/engine/containers/run/#container-identification">identify Docker containers</a>. The UUID might be abbreviated.
    /// </summary>
    public const string AttributeContainerId = "container.id";

    /// <summary>
    /// Runtime specific image identifier. Usually a hash algorithm followed by a UUID.
    /// </summary>
    /// <remarks>
    /// Docker defines a sha256 of the image id; <c>container.image.id</c> corresponds to the <c>Image</c> field from the Docker container inspect <a href="https://docs.docker.com/engine/api/v1.43/#tag/Container/operation/ContainerInspect">API</a> endpoint.
    /// K8s defines a link to the container registry repository with digest <c>"imageID": "registry.azurecr.io /namespace/service/dockerfile@sha256:bdeabd40c3a8a492eaf9e8e44d0ebbb84bac7ee25ac0cf8a7159d25f62555625"</c>.
    /// The ID is assigned by the container runtime and can vary in different environments. Consider using <c>oci.manifest.digest</c> if it is important to identify the same image in different environments/runtimes.
    /// </remarks>
    public const string AttributeContainerImageId = "container.image.id";

    /// <summary>
    /// Name of the image the container was built on.
    /// </summary>
    public const string AttributeContainerImageName = "container.image.name";

    /// <summary>
    /// Repo digests of the container image as provided by the container runtime.
    /// </summary>
    /// <remarks>
    /// <a href="https://docs.docker.com/engine/api/v1.43/#tag/Image/operation/ImageInspect">Docker</a> and <a href="https://github.com/kubernetes/cri-api/blob/c75ef5b473bbe2d0a4fc92f82235efd665ea8e9f/pkg/apis/runtime/v1/api.proto#L1237-L1238">CRI</a> report those under the <c>RepoDigests</c> field.
    /// </remarks>
    public const string AttributeContainerImageRepoDigests = "container.image.repo_digests";

    /// <summary>
    /// Container image tags. An example can be found in <a href="https://docs.docker.com/engine/api/v1.43/#tag/Image/operation/ImageInspect">Docker Image Inspect</a>. Should be only the <c><tag></c> section of the full name for example from <c>registry.example.com/my-org/my-image:<tag></c>.
    /// </summary>
    public const string AttributeContainerImageTags = "container.image.tags";

    /// <summary>
    /// Container labels, <c><key></c> being the label name, the value being the label value.
    /// </summary>
    public const string AttributeContainerLabelTemplate = "container.label";

    /// <summary>
    /// Deprecated, use <c>container.label</c> instead.
    /// </summary>
    [Obsolete("Replaced by <c>container.label</c>.")]
    public const string AttributeContainerLabelsTemplate = "container.labels";

    /// <summary>
    /// Container name used by container runtime.
    /// </summary>
    public const string AttributeContainerName = "container.name";

    /// <summary>
    /// The container runtime managing this container.
    /// </summary>
    public const string AttributeContainerRuntime = "container.runtime";

    /// <summary>
    /// Deprecated, use <c>cpu.mode</c> instead.
    /// </summary>
    public static class ContainerCpuStateValues
    {
        /// <summary>
        /// When tasks of the cgroup are in user mode (Linux). When all container processes are in user mode (Windows).
        /// </summary>
        [Obsolete("Replaced by <c>cpu.mode</c>.")]
        public const string User = "user";

        /// <summary>
        /// When CPU is used by the system (host OS).
        /// </summary>
        [Obsolete("Replaced by <c>cpu.mode</c>.")]
        public const string System = "system";

        /// <summary>
        /// When tasks of the cgroup are in kernel mode (Linux). When all container processes are in kernel mode (Windows).
        /// </summary>
        [Obsolete("Replaced by <c>cpu.mode</c>.")]
        public const string Kernel = "kernel";
    }
}
