// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Resources.Container.Tests;

public class ContainerDetectorTests
{
    private readonly List<TestCase> testValidCasesV1 =
    [
        new(
            name: "cgroupv1 with container Id",
            line: "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            expectedContainerId: "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 with container Id after lines without one",
            line: "13:rdma:/\n12:pids:/user.slice/user-1000.slice/session-2.scope\n11:cpu:/system.slice/docker-d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356.scope",
            expectedContainerId: "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            cgroupVersion: ContainerDetector.ParseMode.V1),

    ];

    private readonly List<TestCase> testValidCasesV2 =
    [
        new(
            name: "cgroupv2 with full line",
            line: "473 456 254:1 /docker/containers/dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw",
            expectedContainerId: "dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183",
            cgroupVersion: ContainerDetector.ParseMode.V2),

        new(
            name: "cgroupv2 with minikube docker mountinfo",
            line: "2327 2307 8:1 /var/lib/docker/containers/a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw",
            expectedContainerId: "a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19",
            cgroupVersion: ContainerDetector.ParseMode.V2),

        new(
            name: "cgroupv2 with minikube docker mountinfo2",
            line: "929 920 254:1 /docker/volumes/minikube/_data/lib/docker/containers/0eaa6718003210b6520f7e82d14b4c8d4743057a958a503626240f8d1900bc33/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw",
            expectedContainerId: "0eaa6718003210b6520f7e82d14b4c8d4743057a958a503626240f8d1900bc33",
            cgroupVersion: ContainerDetector.ParseMode.V2),

        new(
            name: "cgroupv2 with podman mountinfo",
            line: "1096 1088 0:104 /containers/overlay-containers/1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809/userdata/hostname /etc/hostname rw,nosuid,nodev,relatime - tmpfs tmpfs rw,size=813800k,nr_inodes=203450,mode=700,uid=1000,gid=1000",
            expectedContainerId: "1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809",
            cgroupVersion: ContainerDetector.ParseMode.V2),

    ];

    private readonly List<TestCase> testInvalidCases =
    [
        new(
            name: "Invalid cgroupv1 line",
            line: "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23zzzz",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 file of a process that is not in a container",
            line: "0::/user.slice/user-1000.slice/session-2.scope",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "Invalid hex cgroupv2 line (contains a z)",
            line: "13:name=systemd:/var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3fz9320f4402ae6/hostname",
            cgroupVersion: ContainerDetector.ParseMode.V2),

        new(
            name: "cgroupv2 with containerd pod sandbox id (shared by all containers in a pod)",
            line: "1537 1517 8:1 /var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw",
            cgroupVersion: ContainerDetector.ParseMode.V2),

        new(
            name: "cgroupv2 line that is not in mountinfo format",
            line: "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356/hostname",
            cgroupVersion: ContainerDetector.ParseMode.V2),

    ];

    [Fact]
    public void TestValidContainer()
    {
        var containerDetector = new ContainerDetector();
        var allValidTestCases = this.testValidCasesV1.Concat(this.testValidCasesV2);

        foreach (var testCase in allValidTestCases)
        {
            using var tempFile = new TempFile();
            tempFile.Write(testCase.Line);

            var resource = containerDetector.BuildResource(tempFile.FilePath, testCase.CgroupVersion);

            Assert.NotNull(resource);
            Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

            Assert.Equal(testCase.ExpectedContainerId, GetContainerId(resource));
        }
    }

    [Fact]
    public void TestInvalidContainer()
    {
        var containerDetector = new ContainerDetector();
        var missingDockerEnvPath = GetNonExistentFilePath();
        var missingPodmanEnvPath = GetNonExistentFilePath();

        // Valid in cgroupv1 is not valid in cgroupv2
        foreach (var testCase in this.testValidCasesV1)
        {
            using var tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Equal(
                containerDetector.BuildResource(tempFile.FilePath, ContainerDetector.ParseMode.V2, missingDockerEnvPath, missingPodmanEnvPath),
                Resource.Empty);
        }

        // Valid in cgroupv1 is not valid in cgroupv1
        foreach (var testCase in this.testValidCasesV2)
        {
            using var tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Equal(
                containerDetector.BuildResource(tempFile.FilePath, ContainerDetector.ParseMode.V1, missingDockerEnvPath, missingPodmanEnvPath),
                Resource.Empty);
        }

        // test invalid cases
        foreach (var testCase in this.testInvalidCases)
        {
            using var tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Equal(containerDetector.BuildResource(tempFile.FilePath, testCase.CgroupVersion, missingDockerEnvPath, missingPodmanEnvPath), Resource.Empty);
        }

        // test invalid file
        Assert.Equal(containerDetector.BuildResource(Path.GetTempPath(), ContainerDetector.ParseMode.V1, missingDockerEnvPath, missingPodmanEnvPath), Resource.Empty);
        Assert.Equal(containerDetector.BuildResource(Path.GetTempPath(), ContainerDetector.ParseMode.V2, missingDockerEnvPath, missingPodmanEnvPath), Resource.Empty);
    }

    [Fact]
    public void BuildResourceIncludesRuntimeNameWhenContainerIdIsExtractable()
    {
        var containerDetector = new ContainerDetector();
        var testCase = this.testValidCasesV1[0];
        using var tempFile = new TempFile();
        tempFile.Write(testCase.Line);
        using var dockerEnvFile = new TempFile();
        var missingPodmanEnvPath = GetNonExistentFilePath();

        var resource = containerDetector.BuildResource(tempFile.FilePath, testCase.CgroupVersion, dockerEnvFile.FilePath, missingPodmanEnvPath);

        Assert.NotEqual(Resource.Empty, resource);
        Assert.Equal(testCase.ExpectedContainerId, GetContainerId(resource));
        Assert.Equal("docker", GetContainerRuntimeName(resource));
    }

    [Fact]
    public void BuildResourceIncludesRuntimeNameWhenContainerIdIsNotExtractable()
    {
        var containerDetector = new ContainerDetector();
        var testCase = this.testInvalidCases[0];
        using var tempFile = new TempFile();
        tempFile.Write(testCase.Line);
        using var podmanEnvFile = new TempFile();
        var missingDockerEnvPath = GetNonExistentFilePath();

        var resource = containerDetector.BuildResource(tempFile.FilePath, testCase.CgroupVersion, missingDockerEnvPath, podmanEnvFile.FilePath);

        Assert.NotEqual(Resource.Empty, resource);
        Assert.DoesNotContain(resource.Attributes, x => x.Key == ContainerSemanticConventions.AttributeContainerId);
        Assert.Equal("podman", GetContainerRuntimeName(resource));
    }

    [Fact]
    public void BuildResourceReturnsEmptyWhenNoContainerIdOrRuntimeIsDetected()
    {
        var containerDetector = new ContainerDetector();
        var missingDockerEnvPath = GetNonExistentFilePath();
        var missingPodmanEnvPath = GetNonExistentFilePath();

        var resource = containerDetector.BuildResource(Path.GetTempPath(), ContainerDetector.ParseMode.V1, missingDockerEnvPath, missingPodmanEnvPath);

        Assert.Equal(Resource.Empty, resource);
    }

    [Fact]
    public void ContainerDetectorHandlesFailure()
    {
        var resource = ResourceBuilder.CreateEmpty()
            .AddContainerDetector()
            .Build();

        Assert.NotNull(resource);
        Assert.Null(resource.SchemaUrl);
    }

    [Fact]
    public void DetectContainerRuntimeNamePrefersDockerOverPodman()
    {
        using var dockerEnvFile = new TempFile();
        using var podmanEnvFile = new TempFile();

        var runtimeName = ContainerDetector.DetectContainerRuntimeName(dockerEnvFile.FilePath, podmanEnvFile.FilePath);

        Assert.Equal("docker", runtimeName);
    }

    [Fact]
    public void DetectContainerRuntimeNameReturnsPodmanWhenOnlyPodmanEnvFileExists()
    {
        var missingDockerEnvPath = GetNonExistentFilePath();
        using var podmanEnvFile = new TempFile();

        var runtimeName = ContainerDetector.DetectContainerRuntimeName(missingDockerEnvPath, podmanEnvFile.FilePath);

        Assert.Equal("podman", runtimeName);
    }

    [Fact]
    public void DetectContainerRuntimeNameReturnsNullWhenNoMarkerFilesExist()
    {
        var missingDockerEnvPath = GetNonExistentFilePath();
        var missingPodmanEnvPath = GetNonExistentFilePath();

        var runtimeName = ContainerDetector.DetectContainerRuntimeName(missingDockerEnvPath, missingPodmanEnvPath);

        Assert.Null(runtimeName);
    }

    [Fact]
    public void ContainerIdIsReadFromCrioCgroupPathInMountInfo()
    {
        const string Id = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        AssertContainerIdV2(
            Id,
            "1 2 0:25 /kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod1234.slice/crio-" + Id + ".scope /sys/fs/cgroup ro - cgroup2 cgroup2 rw");
    }

    [Fact]
    public void ContainerIdIsReadFromContainerdCgroupPathInMountInfo()
    {
        const string Id = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

        AssertContainerIdV2(
            Id,
            "1 2 0:25 /kubepods-pod1234.slice:cri-containerd:" + Id + " /sys/fs/cgroup ro - cgroup2 cgroup2 rw");
    }

    [Fact]
    public void ContainerIdFromContainerRuntimeCgroupPathIsPreferredOverHostnameMount()
    {
        const string CrioId = "1111111111111111111111111111111111111111111111111111111111111111";
        const string ContainerdId = "2222222222222222222222222222222222222222222222222222222222222222";
        const string ContainersId = "3333333333333333333333333333333333333333333333333333333333333333";

        var hostnameLine = "10 9 8:1 /var/lib/containers/storage/overlay-containers/" + ContainersId + "/userdata/hostname /etc/hostname rw - ext4 /dev/sda1 rw";
        var containerdLine = "11 9 0:25 /slice:cri-containerd:" + ContainerdId + " /sys/fs/cgroup ro - cgroup2 cgroup2 rw";
        var crioLine = "12 9 0:25 /slice/crio-" + CrioId + ".scope /sys/fs/cgroup ro - cgroup2 cgroup2 rw";

        AssertContainerIdV2(ContainerdId, hostnameLine, containerdLine);
        AssertContainerIdV2(CrioId, hostnameLine, containerdLine, crioLine);
    }

    [Fact]
    public void ContainerIdIsTheLastOneFoundInContainersDirectoryOfMountInfo()
    {
        const string FirstId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string LastId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        AssertContainerIdV2(
            LastId,
            "1 2 8:1 /var/lib/docker/containers/" + FirstId + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw",
            "3 2 8:1 /var/lib/docker/containers/" + LastId + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw");
    }

    [Fact]
    public void MultipleContainerIdsInMountInfoLogsWarning()
    {
        const string FirstId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string LastId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        using var listener = new InMemoryEventListener(ContainerExtensionsEventSource.Log);

        AssertContainerIdV2(
            LastId,
            "1 2 8:1 /var/lib/docker/containers/" + FirstId + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw",
            "3 2 8:1 /var/lib/docker/containers/" + LastId + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw");

        var warning = Assert.Single(listener.Events, e => e.EventName == nameof(ContainerExtensionsEventSource.MultipleContainerIdsFound));
        Assert.Equal(EventLevel.Warning, warning.Level);
        Assert.Equal(FirstId + ", " + LastId, Assert.Single(warning.Payload!));
    }

    [Fact]
    public void RepeatedContainerIdInMountInfoDoesNotLogWarning()
    {
        const string Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        using var listener = new InMemoryEventListener(ContainerExtensionsEventSource.Log);

        AssertContainerIdV2(
            Id,
            "1 2 8:1 /var/lib/docker/containers/" + Id + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw",
            "3 2 8:1 /var/lib/docker/containers/" + Id + "/resolv.conf /etc/resolv.conf rw - ext4 /dev/sda1 rw");

        Assert.DoesNotContain(listener.Events, e => e.EventName == nameof(ContainerExtensionsEventSource.MultipleContainerIdsFound));
    }

    [Fact]
    public void ContainerIdFromCrioConmonCgroupPathIsNotUsed()
    {
        const string Id = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        AssertNoContainerIdV2("1 2 0:25 /kubepods.slice/kubepods-pod1234.slice/crio-conmon-" + Id + ".scope /sys/fs/cgroup ro - cgroup2 cgroup2 rw");
    }

    [Fact]
    public void ContainerIdLongerThanExpectedIsNotTruncated()
    {
        const string Id = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        AssertNoContainerIdV2(
            "1 2 0:25 /slice/crio-" + Id + "0 /sys/fs/cgroup ro - cgroup2 cgroup2 rw",
            "2 2 0:25 /slice:cri-containerd:" + Id + "0 /sys/fs/cgroup ro - cgroup2 cgroup2 rw",
            "3 2 8:1 /var/lib/docker/containers/" + Id + "0/hostname /etc/hostname rw - ext4 /dev/sda1 rw");
    }

    [Fact]
    public void ContainerIdIsNotReadFromPodmanVolumeMountRoot()
    {
        const string ContainerId = "1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809";
        const string VolumeId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

        AssertContainerIdV2(
            ContainerId,
            "1 2 0:104 /containers/overlay-containers/" + ContainerId + "/userdata/hostname /etc/hostname rw - tmpfs tmpfs rw",
            "3 2 0:105 /var/lib/containers/storage/volumes/" + VolumeId + "/_data /data rw - ext4 /dev/sda1 rw");
    }

    [Fact]
    public void ContainerIdIsNotReadFromMountTargetOrNonCgroupMount()
    {
        const string DockerId = "a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19";
        const string OtherId = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        AssertContainerIdV2(
            DockerId,
            "1 2 8:1 /var/lib/docker/containers/" + DockerId + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw",
            "3 2 8:1 /data /data/crio-" + OtherId + " rw - cgroup2 cgroup2 rw",
            "4 2 8:1 /slice/crio-" + OtherId + " /data rw - ext4 /dev/sda1 rw",
            "5 2 8:1 /slice:cri-containerd:" + OtherId + " /data rw - ext4 /dev/sda1 rw");
    }

    [Fact]
    public void ContainerIdIsTheLastDistinctIdInOrderOfFirstAppearance()
    {
        const string IdA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string IdB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        // Matches the behavior of the OpenTelemetry Java implementation.
        AssertContainerIdV2(
            IdB,
            "1 2 8:1 /var/lib/docker/containers/" + IdA + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw",
            "2 2 8:1 /var/lib/docker/containers/" + IdB + "/hostname /etc/hosts rw - ext4 /dev/sda1 rw",
            "3 2 8:1 /var/lib/docker/containers/" + IdA + "/resolv.conf /etc/resolv.conf rw - ext4 /dev/sda1 rw");
    }

    [Fact]
    public void SandboxIdOnlyInMountInfoLogsInformationalEvent()
    {
        const string SandboxId = "fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6";

        using var listener = new InMemoryEventListener(ContainerExtensionsEventSource.Log);

        AssertNoContainerIdV2("1537 1517 8:1 /var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/" + SandboxId + "/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw");

        var logged = Assert.Single(listener.Events, e => e.EventName == nameof(ContainerExtensionsEventSource.SandboxContainerIdIgnored));
        Assert.Equal(EventLevel.Informational, logged.Level);
        Assert.Equal(SandboxId, Assert.Single(logged.Payload!));
    }

    [Fact]
    public void SandboxIdWithContainerIdInMountInfoDoesNotLogEvent()
    {
        const string SandboxId = "fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6";
        const string ContainerId = "a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19";

        using var listener = new InMemoryEventListener(ContainerExtensionsEventSource.Log);

        AssertContainerIdV2(
            ContainerId,
            "1 2 8:1 /var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/" + SandboxId + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw",
            "3 2 8:1 /var/lib/docker/containers/" + ContainerId + "/resolv.conf /etc/resolv.conf rw - ext4 /dev/sda1 rw");

        Assert.DoesNotContain(listener.Events, e => e.EventName == nameof(ContainerExtensionsEventSource.SandboxContainerIdIgnored));
    }

    [Fact]
    public void ContainerIdIsNotReadFromSandboxOfPodWhenContainersDirectoryIsPresent()
    {
        const string SandboxId = "fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6";
        const string ContainerId = "a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19";

        AssertContainerIdV2(
            ContainerId,
            "1 2 8:1 /var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/" + SandboxId + "/hostname /etc/hostname rw - ext4 /dev/sda1 rw",
            "3 2 8:1 /var/lib/docker/containers/" + ContainerId + "/resolv.conf /etc/resolv.conf rw - ext4 /dev/sda1 rw");
    }

    private static void AssertNoContainerIdV2(params string[] lines)
    {
        using var tempFile = new TempFile();
        tempFile.Write(string.Join(Environment.NewLine, lines));

        var resource = new ContainerDetector().BuildResource(
            tempFile.FilePath,
            ContainerDetector.ParseMode.V2,
            GetNonExistentFilePath(),
            GetNonExistentFilePath());

        Assert.Equal(Resource.Empty, resource);
    }

    private static void AssertContainerIdV2(string expectedContainerId, params string[] lines)
    {
        using var tempFile = new TempFile();
        tempFile.Write(string.Join(Environment.NewLine, lines));

        var resource = new ContainerDetector().BuildResource(
            tempFile.FilePath,
            ContainerDetector.ParseMode.V2,
            GetNonExistentFilePath(),
            GetNonExistentFilePath());

        Assert.Equal(expectedContainerId, GetContainerId(resource));
    }

    private static string GetNonExistentFilePath() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    private static string GetContainerId(Resource resource)
    {
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        return resourceAttributes[ContainerSemanticConventions.AttributeContainerId].ToString()!;
    }

    private static string GetContainerRuntimeName(Resource resource)
    {
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        return resourceAttributes[ContainerSemanticConventions.AttributeContainerRuntimeName].ToString()!;
    }

    private sealed class TestCase
    {
        public TestCase(string name, string line, ContainerDetector.ParseMode cgroupVersion, string? expectedContainerId = null)
        {
            this.Name = name;
            this.Line = line;
            this.ExpectedContainerId = expectedContainerId;
            this.CgroupVersion = cgroupVersion;
        }

        public string Name { get; }

        public string Line { get; }

        public string? ExpectedContainerId { get; }

        public ContainerDetector.ParseMode CgroupVersion { get; }
    }
}
