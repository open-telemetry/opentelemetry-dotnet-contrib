// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK

namespace OpenTelemetry.Resources.AWS.Tests;

public static class AWSContainerIdTests
{
    private const string Id = "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

    public static TheoryData<string, string?> CgroupFiles() => new()
    {
        //// cgroupfs cgroup driver.
        { $"11:cpu:/kubepods/besteffort/pod1a2b3c4d/{Id}", Id },
        { $"11:cpu:/ecs/55091c13b8a14c4e84b5ef3a9f3e1fd0/{Id}", Id },

        //// systemd cgroup driver.
        { $"11:cpu:/kubepods.slice/kubepods-besteffort.slice/kubepods-besteffort-pod1a2b3c4d.slice/cri-containerd-{Id}.scope", Id },
        { $"11:cpu:/system.slice/docker-{Id}.scope", Id },
        { $"11:devices:/system.slice/containerd.service/kubepods-pod87a18a64_b74a_454a_b10b_a4a36059d0a3.slice:cri-containerd:{Id}", Id },

        //// The first line with a container id is used.
        { $"13:rdma:/\n12:pids:/user.slice/user-1000.slice/user@1000.service/app.slice/app-gnome-code-4521.scope\n11:cpu:/docker/{Id}", Id },

        //// No container id.
        { "0::/", null },
        { "12:pids:/user.slice/user-1000.slice/user@1000.service/app.slice/app-org.gnome.Terminal.slice/vte-spawn-2c4a7f5e-3b1d-4e8a-9f6c-0d1e2f3a4b5c.scope", null },
        { "11:cpu:/ecs/55091c13b8a14c4e84b5ef3a9f3e1fd0/55091c13b8a14c4e84b5ef3a9f3e1fd0-2570125050", null },
    };

    [Theory]
    [MemberData(nameof(CgroupFiles))]
    public static void GetEKSContainerIdParsesCgroupFile(string content, string? expected)
    {
        var path = WriteCgroupFile(content);

        try
        {
            Assert.Equal(expected, AWSEKSDetector.GetEKSContainerId(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(CgroupFiles))]
    public static void GetECSContainerIdParsesCgroupFile(string content, string? expected)
    {
        var path = WriteCgroupFile(content);

        try
        {
            Assert.Equal(expected, AWSECSDetector.GetECSContainerId(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteCgroupFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content + "\n");
        return path;
    }
}

#endif
