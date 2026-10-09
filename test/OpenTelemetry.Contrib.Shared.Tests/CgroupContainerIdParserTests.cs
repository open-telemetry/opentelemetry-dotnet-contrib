// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Internal.Tests;

public class CgroupContainerIdParserTests
{
    private const string Id = "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

    public static TheoryData<string, string> LinesWithContainerId() => new()
    {
        //// Just the container id.
        { $"13:name=systemd:/pod/{Id}", Id },
        { $"14:name=systemd:/docker/{Id}", Id },
        { $"11:cpu:/kubepods/besteffort/pod1a2b3c4d/{Id}", Id },
        { $"11:cpu:/ecs/55091c13b8a14c4e84b5ef3a9f3e1fd0/{Id}", Id },

        //// With a prefix and/or a suffix.
        { $"13:name=systemd:/podruntime/docker/kubepods/crio-{Id}", Id },
        { $"13:name=systemd:/podruntime/docker/kubepods/{Id}.aaaa", Id },
        { $"13:name=systemd:/podruntime/docker/kubepods/crio-{Id}.stuff", Id },

        //// The systemd cgroup driver, as used by container runtimes.
        { $"0::/system.slice/docker-{Id}.scope", Id },
        { $"0::/kubepods.slice/kubepods-besteffort.slice/kubepods-besteffort-pod1a2b3c4d.slice/cri-containerd-{Id}.scope", Id },
        { $"11:perf_event:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod4415fd05_2c0f_4533_909b_f2180dca8d7c.slice/cri-containerd-{Id}.scope", Id },
        { $"0::/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod1a2b3c4d.slice/crio-{Id}.scope", Id },
        { $"0::/machine.slice/libpod-{Id}.scope", Id },

        //// runc's default cgroup path when using the systemd cgroup driver (":runc:<id>").
        { $"0::/system.slice/runc-{Id}.scope", Id },
        { $"0::/user.slice/user-1000.slice/user@1000.service/user.slice/runc-{Id}.scope", Id },

        //// A runtime configured with a custom prefix ("<parent>:<prefix>:<id>").
        { $"0::/system.slice/my-runtime-{Id}.scope", Id },

        //// containerd v1.5+ with the systemd cgroup driver separates the container id with a colon.
        { $"11:devices:/system.slice/containerd.service/kubepods-pod87a18a64_b74a_454a_b10b_a4a36059d0a3.slice:cri-containerd:{Id}", Id },
    };

    public static TheoryData<string> LinesWithoutContainerId() =>
    [
        //// Not a cgroup path.
        string.Empty,
        "not a cgroup line",

        //// The root cgroup or a cgroup without a container id.
        "0::/",
        "0::/init.scope",
        "0::/system.slice/nginx.service",

        //// SSH / TTY login sessions on cgroup v2 and cgroup v1 / hybrid hosts.
        "0::/user.slice/user-1000.slice/session-2.scope",
        "12:pids:/user.slice/user-1000.slice/session-c2.scope",
        "1:name=systemd:/user.slice/user-1001.slice/session-2.scope",
        "1:name=systemd:/user.slice/user-0.slice/session-31207.scope",

        //// An ordinary systemd service whose unit name happens to be hexadecimal.
        "0::/system.slice/db.service",

        //// Processes started from desktop applications.
        "0::/user.slice/user-1000.slice/user@1000.service/app.slice/app-org.gnome.Terminal.slice/vte-spawn-2c4a7f5e-3b1d-4e8a-9f6c-0d1e2f3a4b5c.scope",
        "0::/user.slice/user-1000.slice/user@1000.service/app.slice/app-gnome-code-4521.scope",

        //// A unit with an unknown prefix whose name does not contain a full-length container id.
        "0::/system.slice/custom-abc123.scope",

        //// cgroups whose names happen to be (short) hexadecimal.
        "0::/system.slice/cafe",
        "0::/kubepods/besteffort/pod1/abc",
        "4:cpu:/lxc/123",

        //// An ECS Fargate task, whose container ids are not 64 characters long.
        "11:cpu:/ecs/55091c13b8a14c4e84b5ef3a9f3e1fd0/55091c13b8a14c4e84b5ef3a9f3e1fd0-2570125050",

        //// A container id that is not hexadecimal.
        "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23zzzz",
        $"13:name=systemd:/pod/{Id.Substring(0, 63)}z",

        //// A container id that is too short or too long.
        $"13:name=systemd:/pod/{Id.Substring(0, 63)}",
        $"13:name=systemd:/pod/{Id}a",
        $"13:name=systemd:/podruntime/docker/kubepods/crio-{Id.Substring(0, 36)}",

        //// An unrecognized format (the last '-' is after the last '.').
        "13:name=systemd:/podruntime/docker/kubepods/ac679f8.a8319c8cf7d38e1adf263bc08-d23zzzz",
        $"13:name=systemd:/pod/abc.{Id}-def",

        //// The container id is not in the last section of the path.
        $"13:name=systemd:/docker/{Id}/child",
        $"11:devices:/system.slice/containerd.service/kubepods.slice:cri-containerd:{Id}:extra",
    ];

    [Theory]
    [MemberData(nameof(LinesWithContainerId))]
    public void GetContainerIdReturnsContainerId(string line, string expected)
        => Assert.Equal(expected, CgroupContainerIdParser.GetContainerId(line));

    [Theory]
    [MemberData(nameof(LinesWithoutContainerId))]
    public void GetContainerIdReturnsNullWhenLineHasNoContainerId(string line)
        => Assert.Null(CgroupContainerIdParser.GetContainerId(line));
}
