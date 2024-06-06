// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace OpenTelemetry.Resources.Container.Tests;

public class ContainerDetectorTests
{
    private readonly List<TestCase> testValidCasesV1 = new()
    {
        new(
            name: "cgroupv1 with prefix",
            line: "13:name=systemd:/podruntime/docker/kubepods/crio-e2cc29debdf85dde404998aa128997a819ff",
            expectedContainerId: "e2cc29debdf85dde404998aa128997a819ff",
            cgroupVersion: ParseMode.V1),
        new(
            name: "cgroupv1 with suffix",
            line: "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23.aaaa",
            expectedContainerId: "ac679f8a8319c8cf7d38e1adf263bc08d23",
            cgroupVersion: ParseMode.V1),
        new(
            name: "cgroupv1 with prefix and suffix",
            line: "13:name=systemd:/podruntime/docker/kubepods/crio-dc679f8a8319c8cf7d38e1adf263bc08d23.stuff",
            expectedContainerId: "dc679f8a8319c8cf7d38e1adf263bc08d23",
            cgroupVersion: ParseMode.V1),
        new(
            name: "cgroupv1 with container Id",
            line: "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            expectedContainerId: "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            cgroupVersion: ParseMode.V1),
    };

    private readonly List<TestCase> testValidCasesV2 = new()
    {
        new(
            name: "cgroupv2 with container Id",
            line: "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356/hostname",
            expectedContainerId: "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356",
            cgroupVersion: ParseMode.V2),
        new(
            name: "cgroupv2 with full line",
            line: "473 456 254:1 /docker/containers/dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw",
            expectedContainerId: "dc64b5743252dbaef6e30521c34d6bbd1620c8ce65bdb7bf9e7143b61bb5b183",
            cgroupVersion: ParseMode.V2),
        new(
            name: "cgroupv2 with minikube containerd mountinfo",
            line: "1537 1517 8:1 /var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw",
            expectedContainerId: "fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3ff9320f4402ae6",
            cgroupVersion: ParseMode.V2),
        new(
            name: "cgroupv2 with minikube docker mountinfo",
            line: "2327 2307 8:1 /var/lib/docker/containers/a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19/hostname /etc/hostname rw,relatime - ext4 /dev/sda1 rw",
            expectedContainerId: "a1551a1d7e1881d6c18d2c9ec462cab6ad3666825f0adb2098e9d5b198fd7e19",
            cgroupVersion: ParseMode.V2),
        new(
            name: "cgroupv2 with minikube docker mountinfo2",
            line: "929 920 254:1 /docker/volumes/minikube/_data/lib/docker/containers/0eaa6718003210b6520f7e82d14b4c8d4743057a958a503626240f8d1900bc33/hostname /etc/hostname rw,relatime - ext4 /dev/vda1 rw",
            expectedContainerId: "0eaa6718003210b6520f7e82d14b4c8d4743057a958a503626240f8d1900bc33",
            cgroupVersion: ParseMode.V2),
        new(
            name: "cgroupv2 with podman mountinfo",
            line: "1096 1088 0:104 /containers/overlay-containers/1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809/userdata/hostname /etc/hostname rw,nosuid,nodev,relatime - tmpfs tmpfs rw,size=813800k,nr_inodes=203450,mode=700,uid=1000,gid=1000",
            expectedContainerId: "1a2de27e7157106568f7e081e42a8c14858c02bd9df30d6e352b298178b46809",
            cgroupVersion: ParseMode.V2),
    };

    private readonly List<TestCase> testInvalidCases = new()
    {
        new(
            name: "Invalid cgroupv1 line",
            line: "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23zzzz",
            cgroupVersion: ParseMode.V1),
        new(
            name: "Invalid hex cgroupv2 line (contains a z)",
            line: "13:name=systemd:/var/lib/containerd/io.containerd.grpc.v1.cri/sandboxes/fb5916a02feca96bdeecd8e062df9e5e51d6617c8214b5e1f3fz9320f4402ae6/hostname",
            cgroupVersion: ParseMode.V2),
    };

    [Fact]
    public void TestValidContainer()
    {
        var containerDetector = new ContainerDetector();
        var allValidTestCases = this.testValidCasesV1.Concat(this.testValidCasesV2);

        foreach (var testCase in allValidTestCases)
        {
            using var tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Equal(
                testCase.ExpectedContainerId,
                containerDetector.ExtractContainerId(tempFile.FilePath, testCase.CgroupVersion));
        }
    }

    [Fact]
    public void TestInvalidContainer()
    {
        var containerDetector = new ContainerDetector();

        // Valid in cgroupv1 is not valid in cgroupv2
        foreach (var testCase in this.testValidCasesV1)
        {
            using var tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Null(
                containerDetector.ExtractContainerId(tempFile.FilePath, ParseMode.V2));
        }

        // Valid in cgroupv1 is not valid in cgroupv1
        foreach (var testCase in this.testValidCasesV2)
        {
            using var tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Null(
                containerDetector.ExtractContainerId(tempFile.FilePath, ParseMode.V1));
        }

        // test invalid cases
        foreach (var testCase in this.testInvalidCases)
        {
            using var tempFile = new TempFile();
            tempFile.Write(testCase.Line);
            Assert.Null(containerDetector.ExtractContainerId(tempFile.FilePath, testCase.CgroupVersion));
        }

        // test invalid file
        Assert.Null(containerDetector.ExtractContainerId(Path.GetTempPath(), ParseMode.V1));
        Assert.Null(containerDetector.ExtractContainerId(Path.GetTempPath(), ParseMode.V2));
    }

    [Fact]
    public async Task TestK8sContainerId()
    {
        await using (_ = new MockK8sEndpoint("k8s/pod-response.json"))
        {
            var resourceAttributes = new ContainerDetector(new MockK8sMetadataFetcher()).Detect().Attributes.ToDictionary(x => x.Key, x => x.Value);

            Assert.Equal(resourceAttributes[ContainerSemanticConventions.AttributeContainerId], "96724c05fa1be8d313f6db0e9872ca542b076839c4fd51ea4912a670ef538cbd");
        }
    }

    private sealed class TestCase
    {
        public TestCase(string name, string line, ParseMode cgroupVersion, string? expectedContainerId = null)
        {
            this.Name = name;
            this.Line = line;
            this.ExpectedContainerId = expectedContainerId;
            this.CgroupVersion = cgroupVersion;
        }

        public string Name { get; }

        public string Line { get; }

        public string? ExpectedContainerId { get; }

        public ParseMode CgroupVersion { get; }
    }

    private class MockK8sEndpoint : IAsyncDisposable
    {
        public readonly Uri Address;
        private readonly IWebHost server;

        public MockK8sEndpoint(string responseJsonPath)
        {
            this.server = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://127.0.0.1:5000")
                .Configure(app =>
            {
                app.Run(async context =>
                {
                    if (context.Request.Method == HttpMethods.Get && context.Request.Path == "/api/v1/namespaces/default/pods/pod1")
                    {
                        var content = await File.ReadAllTextAsync($"{Environment.CurrentDirectory}/{responseJsonPath}");
                        var data = Encoding.UTF8.GetBytes(content);
                        context.Response.ContentType = "application/json";
                        await context.Response.Body.WriteAsync(data);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsync("Not found");
                    }
                });
            }).Build();
            this.server.Start();

            this.Address = new Uri(this.server.ServerFeatures.Get<IServerAddressesFeature>()!.Addresses.First());
        }

        public async ValueTask DisposeAsync()
        {
            await this.DisposeAsyncCore();
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            await this.server.StopAsync();
        }
    }

    private sealed class MockK8sMetadataFetcher : IK8sMetadataFetcher
    {
        public string? GetApiCredential()
        {
            return Guid.NewGuid().ToString();
        }

        public string? GetContainerName()
        {
            return "my-container";
        }

        public string? GetHostname()
        {
            return "hostname";
        }

        public string? GetNamespace()
        {
            return "default";
        }

        public string? GetPodName()
        {
            return "pod1";
        }

        public string? GetServiceBaseUrl()
        {
            return "http://127.0.0.1:5000";
        }
    }
}
