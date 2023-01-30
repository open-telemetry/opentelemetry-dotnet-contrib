// <copyright file="DockerResourceDetectorTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTelemetry.Extensions.Docker.Resources;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Extensions.Docker.Tests;

public class DockerResourceDetectorTests
{
    private readonly List<TestCase> testValidCasesV1 = new()
    {
        new TestCase()
        {
            Name = "cgroupv1 with prefix",
            Line = "13:name=systemd:/podruntime/docker/kubepods/crio-e2cc29debdf85dde404998aa128997a819ff",
            ExpectedContainerId = "e2cc29debdf85dde404998aa128997a819ff",
            CgroupVersion = DockerResourceDetector.ParseMode.V1,
        },
        new TestCase()
        {
            Name = "cgroupv1 with suffix",
            Line = "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23.aaaa",
            ExpectedContainerId = "ac679f8a8319c8cf7d38e1adf263bc08d23",
            CgroupVersion = DockerResourceDetector.ParseMode.V1,
        },
        new TestCase()
        {
            Name = "cgroupv1 with prefix and suffix",
            Line = "13:name=systemd:/podruntime/docker/kubepods/crio-dc679f8a8319c8cf7d38e1adf263bc08d23.stuff",
            ExpectedContainerId = "dc679f8a8319c8cf7d38e1adf263bc08d23",
            CgroupVersion = DockerResourceDetector.ParseMode.V1,
        },
        new TestCase()
        {
            Name = "cgroupv1 with container Id",
            Line = "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            ExpectedContainerId = "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            CgroupVersion = DockerResourceDetector.ParseMode.V1,
        },
    };

    private readonly List<TestCase> testValidCasesV2 = new()
    {
        new TestCase()
        {
            Name = "cgroupv2 with container Id",
            Line = "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356/hostname",
            ExpectedContainerId = "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            CgroupVersion = DockerResourceDetector.ParseMode.V2,
        },
        new TestCase()
        {
            Name = "cgroupv2 with full line",
            Line = "473 456 254:1 /docker/containers/dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw",
            ExpectedContainerId = "dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183",
            CgroupVersion = DockerResourceDetector.ParseMode.V2,
        },
        new TestCase()
        {
            Name = "cgroupv2 with minikube containerd mountinfo",
            Line = "1537 1517 8:1 /var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw",
            ExpectedContainerId = "fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6",
            CgroupVersion = DockerResourceDetector.ParseMode.V2,
        },
        new TestCase()
        {
            Name = "cgroupv2 with minikube docker mountinfo",
            Line = "2327 2307 8:1 /var/lib/docker/containers/a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw",
            ExpectedContainerId = "a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19",
            CgroupVersion = DockerResourceDetector.ParseMode.V2,
        },
        new TestCase()
        {
            Name = "cgroupv2 with minikube docker mountinfo2",
            Line = "929 920 254:1 /docker/volumes/minikube/_data/lib/docker/containers/0eaa6718003210b6520f7e82d14b4c8d4743057a958a503626240f8d1900bc33/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw",
            ExpectedContainerId = "0eaa6718003210b6520f7e82d14b4c8d4743057a958a503626240f8d1900bc33",
            CgroupVersion = DockerResourceDetector.ParseMode.V2,
        },
        new TestCase()
        {
            Name = "cgroupv2 with podman mountinfo",
            Line = "1096 1088 0:104 /containers/overlay-containers/1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809/userdata/hostname /etc/hostname rw,nosuid,nodev,relatime - tmpfs tmpfs rw,size=813800k,nr_inodes=203450,mode=700,uid=1000,gid=1000",
            ExpectedContainerId = "1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809",
            CgroupVersion = DockerResourceDetector.ParseMode.V2,
        },
    };

    private readonly List<TestCase> testInvalidCases = new()
    {
        new TestCase()
        {
            Name = "Invalid cgroupv1 line",
            Line = "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23zzzz",
            CgroupVersion = DockerResourceDetector.ParseMode.V1,
        },
        new TestCase()
        {
            Name = "Invalid hex cgroupv2 line (contains a z)",
            Line = "13:name=systemd:/var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3fz9320f4402ae6/hostname",
            CgroupVersion = DockerResourceDetector.ParseMode.V2,
        },
    };

    [Fact]
    public void TestValidContainer()
    {
        var dockerResourceDetector = new DockerResourceDetector();
        var allValidTestCases = this.testValidCasesV1.Concat(this.testValidCasesV2);

        foreach (var testCase in allValidTestCases)
        {
            using TempFile tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Equal(
                testCase.ExpectedContainerId,
                this.GetContainerId(dockerResourceDetector.BuildResource(tempFile.FilePath, testCase.CgroupVersion)));
        }
    }

    [Fact]
    public void TestInvalidContainer()
    {
        var dockerResourceDetector = new DockerResourceDetector();

        // Valid in cgroupv1 is not valid in cgroupv2
        foreach (var testCase in this.testValidCasesV1)
        {
            using TempFile tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Equal(
                dockerResourceDetector.BuildResource(tempFile.FilePath, DockerResourceDetector.ParseMode.V2),
                Resource.Empty);
        }

        // Valid in cgroupv1 is not valid in cgroupv1
        foreach (var testCase in this.testValidCasesV2)
        {
            using TempFile tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Equal(
                dockerResourceDetector.BuildResource(tempFile.FilePath, DockerResourceDetector.ParseMode.V1),
                Resource.Empty);
        }

        // test invalid cases
        foreach (var testCase in this.testInvalidCases)
        {
            using TempFile tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Equal(dockerResourceDetector.BuildResource(tempFile.FilePath, testCase.CgroupVersion), Resource.Empty);
        }

        // test invalid file
        Assert.Equal(dockerResourceDetector.BuildResource(Path.GetTempPath(), DockerResourceDetector.ParseMode.V1), Resource.Empty);
        Assert.Equal(dockerResourceDetector.BuildResource(Path.GetTempPath(), DockerResourceDetector.ParseMode.V2), Resource.Empty);
    }

    private string GetContainerId(Resource resource)
    {
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        return resourceAttributes[DockerSemanticConventions.AttributeContainerID]?.ToString();
    }

    private class TestCase
    {
        public string Name { get; set; }

        public string Line { get; set; }

        public string ExpectedContainerId { get; set; }

        public DockerResourceDetector.ParseMode CgroupVersion { get; set; }
    }
}
