// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Container.Tests;

public class ContainerIdSystemdUnitTests
{
    private const string Id = "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

    public static TheoryData<string> NonContainerCgroupLines() =>
    [
        //// SSH / TTY login sessions on cgroup v2 and cgroup v1 / hybrid hosts.
        "0::/user.slice/user-1000.slice/session-2.scope",
        "12:pids:/user.slice/user-1000.slice/session-c2.scope",
        "1:name=systemd:/user.slice/user-1001.slice/session-2.scope",

        //// An ordinary systemd service whose unit name happens to be hexadecimal.
        "0::/system.slice/db.service",

        //// Processes started from desktop applications.
        "0::/user.slice/user-1000.slice/user@1000.service/app.slice/app-org.gnome.Terminal.slice/vte-spawn-2c4a7f5e-3b1d-4e8a-9f6c-0d1e2f3a4b5c.scope",
        "0::/user.slice/user-1000.slice/user@1000.service/app.slice/app-gnome-code-4521.scope",

        //// A unit with an unknown prefix whose name does not contain a full-length container id.
        "0::/system.slice/custom-abc123.scope",

        //// Non-systemd cgroups whose names happen to be (short) hexadecimal.
        "0::/system.slice/cafe",
        "0::/kubepods/besteffort/pod1/abc",
        "4:cpu:/lxc/123",
    ];

    public static TheoryData<string> ContainerCgroupLines() =>
    [
        $"0::/system.slice/docker-{Id}.scope",
        $"0::/kubepods.slice/kubepods-besteffort.slice/kubepods-besteffort-pod1a2b3c4d.slice/cri-containerd-{Id}.scope",
        $"0::/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod1a2b3c4d.slice/crio-{Id}.scope",
        $"0::/machine.slice/libpod-{Id}.scope",

        //// runc's default cgroup path when using the systemd cgroup driver (":runc:<id>").
        $"0::/system.slice/runc-{Id}.scope",
        $"0::/user.slice/user-1000.slice/user@1000.service/user.slice/runc-{Id}.scope",

        //// A runtime configured with a custom prefix ("<parent>:<prefix>:<id>").
        $"0::/system.slice/my-runtime-{Id}.scope",
    ];

    [Theory]
    [MemberData(nameof(NonContainerCgroupLines))]
    public void SystemdUnitOfProcessOutsideContainerIsNotReportedAsContainerId(string cgroupLine)
    {
        var resource = DetectFromCgroupFile(cgroupLine);

        Assert.DoesNotContain(resource.Attributes, a => a.Key == ContainerSemanticConventions.AttributeContainerId);
    }

    [Theory]
    [MemberData(nameof(ContainerCgroupLines))]
    public void SystemdUnitCreatedByContainerRuntimeIsReportedAsContainerId(string cgroupLine)
    {
        var resource = DetectFromCgroupFile(cgroupLine);

        Assert.Contains(new KeyValuePair<string, object>(ContainerSemanticConventions.AttributeContainerId, Id), resource.Attributes);
    }

    [Theory]
    [InlineData("0::/system.slice/nginx.service")]
    [InlineData("0::/init.scope")]
    [InlineData("0::/")]
    public void CgroupPathsWithoutContainerIdProduceNoContainerId(string cgroupLine)
    {
        var resource = DetectFromCgroupFile(cgroupLine);

        Assert.DoesNotContain(resource.Attributes, a => a.Key == ContainerSemanticConventions.AttributeContainerId);
    }

    private static Resource DetectFromCgroupFile(string cgroupContent)
    {
        using var cgroupFile = new TempFile();
        cgroupFile.Write(cgroupContent + "\n");

        // Point the runtime marker files at paths that do not exist so only the cgroup parsing is exercised.
        var missingDockerEnv = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var missingPodmanEnv = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        return new ContainerDetector().BuildResource(cgroupFile.FilePath, ContainerDetector.ParseMode.V1, missingDockerEnv, missingPodmanEnv);
    }
}
