// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.Container.Tests;

public class ContainerDetectorTests
{
    private readonly List<TestCase> testValidCasesV1 =
    [
        new(
            name: "cgroupv1 with prefix",
            line: "13:name=systemd:/podruntime/docker/kubepods/crio-e2cc29debdf85dde404998aa128997a819ff991827356a1b2c3d4e5f60718293",
            expectedContainerId: "e2cc29debdf85dde404998aa128997a819ff991827356a1b2c3d4e5f60718293",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 with suffix",
            line: "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d231f2ff81abda3915f6e8ba4d64156a.aaaa",
            expectedContainerId: "ac679f8a8319c8cf7d38e1adf263bc08d231f2ff81abda3915f6e8ba4d64156a",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 with prefix and suffix",
            line: "13:name=systemd:/podruntime/docker/kubepods/crio-dc679f8a8319c8cf7d38e1adf263bc08d234f0749ea715fb6ca3bb259db69956.stuff",
            expectedContainerId: "dc679f8a8319c8cf7d38e1adf263bc08d234f0749ea715fb6ca3bb259db69956",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 with container Id",
            line: "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            expectedContainerId: "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 with two dashes in prefix",
            line: "11:perf_event:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod4415fd05_2c0f_4533_909b_f2180dca8d7c.slice/cri-containerd-713a77a26fe2a38ebebd5709604a048c3d380db1eb16aa43aca0b2499e54733c.scope",
            expectedContainerId: "713a77a26fe2a38ebebd5709604a048c3d380db1eb16aa43aca0b2499e54733c",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 with colon (containerd v1.5+ with systemd cgroup driver)",
            line: "11:devices:/system.slice/containerd.service/kubepods-pod87a18a64_b74a_454a_b10b_a4a36059d0a3.slice:cri-containerd:05c48c82caff3be3d7f1e896981dd410e81487538936914f32b624d168de9db0",
            expectedContainerId: "05c48c82caff3be3d7f1e896981dd410e81487538936914f32b624d168de9db0",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 with unrecognized line before container id",
            line: "13:name=systemd:/podruntime/docker/kubepods/ac679f8.a8319c8cf7d38e1adf263bc08-d23zzzz\n0::/system.slice/docker-d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356.scope",
            expectedContainerId: "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            cgroupVersion: ContainerDetector.ParseMode.V1),

    ];

    private readonly List<TestCase> testValidCasesV2 =
    [
        new(
            name: "cgroupv2 with container Id",
            line: "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356/hostname",
            expectedContainerId: "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            cgroupVersion: ContainerDetector.ParseMode.V2),

        new(
            name: "cgroupv2 with full line",
            line: "473 456 254:1 /docker/containers/dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw",
            expectedContainerId: "dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183",
            cgroupVersion: ContainerDetector.ParseMode.V2),

        new(
            name: "cgroupv2 with minikube containerd mountinfo",
            line: "1537 1517 8:1 /var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw",
            expectedContainerId: "fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6",
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
            cgroupVersion: ContainerDetector.ParseMode.V2)

    ];

    private readonly List<TestCase> testInvalidCases =
    [
        new(
            name: "Invalid cgroupv1 line",
            line: "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23zzzz",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 line with id that is too short",
            line: "13:name=systemd:/podruntime/docker/kubepods/crio-e2cc29debdf85dde404998aa128997a819ff",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 line with id that is too long",
            line: "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356a",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "cgroupv1 line with unrecognized format (last '-' is after last '.')",
            line: "13:name=systemd:/podruntime/docker/kubepods/ac679f8.a8319c8cf7d38e1adf263bc08-d23zzzz",
            cgroupVersion: ContainerDetector.ParseMode.V1),

        new(
            name: "Invalid hex cgroupv2 line (contains a z)",
            line: "13:name=systemd:/var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3fz9320f4402ae6/hostname",
            cgroupVersion: ContainerDetector.ParseMode.V2)

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
