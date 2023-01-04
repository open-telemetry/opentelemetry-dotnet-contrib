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

using System.IO;
using System.Linq;
using OpenTelemetry.Extensions.Docker.Resources;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Extensions.Docker.Tests;

public class DockerResourceDetectorTests
{
    // Invalid cgroup line
    private const string INVALIDCGROUPLINE =
        "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23zzzz";

    // cgroup line with prefix
    private const string CGROUPLINEWITHPREFIX =
        "13:name=systemd:/podruntime/docker/kubepods/crio-e2cc29debdf85dde404998aa128997a819ff";

    // Expected Container Id with prefix removed
    private const string CONTAINERIDWITHPREFIXREMOVED = "e2cc29debdf85dde404998aa128997a819ff";

    // cgroup line with suffix
    private const string CGROUPLINEWITHSUFFIX =
        "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23.aaaa";

    // Expected Container Id with suffix removed
    private const string CONTAINERIDWITHSUFFIXREMOVED = "ac679f8a8319c8cf7d38e1adf263bc08d23";

    // cgroup line with prefix and suffix
    private const string CGROUPLINEWITHPREFIXandSUFFIX =
        "13:name=systemd:/podruntime/docker/kubepods/crio-dc679f8a8319c8cf7d38e1adf263bc08d23.stuff";

    // Expected Container Id with both prefix and suffix removed
    private const string CONTAINERIDWITHPREFIXANDSUFFIXREMOVED = "dc679f8a8319c8cf7d38e1adf263bc08d23";

    // cgroup line with container Id
    private const string CGROUPLINE =
        "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

    // Expected Container Id
    private const string CONTAINERID =
        "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

    // cgroupv2 line with container Id
    private const string CGROUPLINEV2 =
        "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356/hostname";

    // cgroupv2 Expected Container Id 
    private const string CONTAINERIDV2 = "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

    // hostname
    private const string CGROUPLINEV2WITHHOSTNAME =
        "473 456 254:1 /docker/containers/dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw";

    private const string CONTAINERIDV2WITHHOSTNAME = "dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183";

    // minikube containerd mountinfo
    private const string CGROUPLINEV2WITHMINIKUBECONTAINERD =
        "1537 1517 8:1 /var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw";

    private const string CONTAINERIDV2WITHMINIKUBECONTAINERD = "fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6";

    // minikube docker mountinfo
    private const string CGROUPLINEV2WITHDOCKER =
        "2327 2307 8:1 /var/lib/docker/containers/a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw";

    private const string CONTAINERIDV2WITHDOCKER = "a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19";

    // minikube docker mountinfo2
    private const string CGROUPLINEV2WITHDOCKER2 =
        "929 920 254:1 /docker/volumes/minikube/_data/lib/docker/containers/0eaa6718003210b6520f7e82d14b4c8d4743057a958a503626240f8d1900bc33/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw";

    private const string CONTAINERIDV2WITHDOCKER2 = "0eaa6718003210b6520f7e82d14b4c8d4743057a958a503626240f8d1900bc33";

    // podman mountinfo
    private const string CGROUPLINEV2WITHPODMAN =
        "1096 1088 0:104 /containers/overlay-containers/1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809/userdata/hostname /etc/hostname rw,nosuid,nodev,relatime - tmpfs tmpfs rw,size=813800k,nr_inodes=203450,mode=700,uid=1000,gid=1000";

    private const string CONTAINERIDV2WITHPODMAN = "1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809";

    // Invalid cgroup line (contains a z)"
    private const string INVALIDHEXCGROUPLINEV2 =
        "13:name=systemd:/var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3fz9320f4402ae6/hostname";

    [Fact]
    public void TestValidContainerV1()
    {
        var dockerResourceDetector = new DockerResourceDetector();

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEWITHPREFIX);
            Assert.Equal(CONTAINERIDWITHPREFIXREMOVED, this.GetContainerId(dockerResourceDetector.BuildResourceV1(tempFile.FilePath)));
        }

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEWITHSUFFIX);
            Assert.Equal(CONTAINERIDWITHSUFFIXREMOVED, this.GetContainerId(dockerResourceDetector.BuildResourceV1(tempFile.FilePath)));
        }

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEWITHPREFIXandSUFFIX);
            Assert.Equal(CONTAINERIDWITHPREFIXANDSUFFIXREMOVED, this.GetContainerId(dockerResourceDetector.BuildResourceV1(tempFile.FilePath)));
        }

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINE);
            Assert.Equal(CONTAINERID, this.GetContainerId(dockerResourceDetector.BuildResourceV1(tempFile.FilePath)));
        }
    }

    [Fact]
    public void TestValidContainerV2()
    {
        var dockerResourceDetector = new DockerResourceDetector();

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEV2);
            Assert.Equal(CONTAINERIDV2, this.GetContainerId(dockerResourceDetector.BuildResourceV2(tempFile.FilePath)));
        }

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEV2WITHHOSTNAME);
            Assert.Equal(CONTAINERIDV2WITHHOSTNAME, this.GetContainerId(dockerResourceDetector.BuildResourceV2(tempFile.FilePath)));
        }

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEV2WITHMINIKUBECONTAINERD);
            Assert.Equal(CONTAINERIDV2WITHMINIKUBECONTAINERD, this.GetContainerId(dockerResourceDetector.BuildResourceV2(tempFile.FilePath)));
        }

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEV2WITHDOCKER);
            Assert.Equal(CONTAINERIDV2WITHDOCKER, this.GetContainerId(dockerResourceDetector.BuildResourceV2(tempFile.FilePath)));
        }

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEV2WITHDOCKER2);
            Assert.Equal(CONTAINERIDV2WITHDOCKER2, this.GetContainerId(dockerResourceDetector.BuildResourceV2(tempFile.FilePath)));
        }

        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(CGROUPLINEV2WITHPODMAN);
            Assert.Equal(CONTAINERIDV2WITHPODMAN, this.GetContainerId(dockerResourceDetector.BuildResourceV2(tempFile.FilePath)));
        }
    }

    [Fact]
    public void TestInvalidContainerV1()
    {
        var dockerResourceDetector = new DockerResourceDetector();

        // test invalid containerId (non-hex)
        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(INVALIDCGROUPLINE);
            Assert.Equal(dockerResourceDetector.BuildResourceV1(tempFile.FilePath), Resource.Empty);
        }

        // test invalid file
        Assert.Equal(dockerResourceDetector.BuildResourceV1(Path.GetTempPath()), Resource.Empty);
    }

    [Fact]
    public void TestInvalidContainerV2()
    {
        var dockerResourceDetector = new DockerResourceDetector();

        // test invalid containerId (non-hex)
        using (TempFile tempFile = new TempFile())
        {
            tempFile.Write(INVALIDHEXCGROUPLINEV2);
            Assert.Equal(dockerResourceDetector.BuildResourceV1(tempFile.FilePath), Resource.Empty);
        }

        // test invalid file
        Assert.Equal(dockerResourceDetector.BuildResourceV1(Path.GetTempPath()), Resource.Empty);
    }

    private string GetContainerId(Resource resource)
    {
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        return resourceAttributes[DockerSemanticConventions.AttributeContainerID]?.ToString();
    }
}
