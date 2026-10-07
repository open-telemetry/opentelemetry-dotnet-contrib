// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK

namespace OpenTelemetry.Resources.AWS.Tests;

public static class AWSContainerIdTests
{
    private const string Id = "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

    public static TheoryData<string, string?> CgroupFiles() => new()
    {
        { $"11:cpu:/docker/{Id}", Id },

        //// The first line with a container id is used.
        { $"13:rdma:/\n12:pids:/user.slice/user-1000.slice/session-2.scope\n11:cpu:/system.slice/docker-{Id}.scope\n10:memory:/docker/{Id.Replace('d', 'e')}", Id },

        //// No container id.
        { "0::/", null },
        { "13:rdma:/\n12:pids:/user.slice/user-1000.slice/session-2.scope", null },
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
