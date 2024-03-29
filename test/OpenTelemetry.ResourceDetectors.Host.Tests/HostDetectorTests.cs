// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.Host.Tests;

public class HostDetectorTests
{
    [Fact]
    public void TestHostAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddDetector(new HostDetector()).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal(2, resourceAttributes.Count);

        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

    [Fact]
    public void TestHostMachineId()
    {
        var etcMachineIdStream = (string path) =>
        {
            return path == "/etc/machine-id"
                ? new MemoryStream(Encoding.UTF8.GetBytes("etc-machine-id"))
                : null;
        };
        var varLibDbusMachineIdStream = new MemoryStream(Encoding.UTF8.GetBytes("var-lib-dbus-machine-id"));
    }

    [Fact]
    public void TestHostMachineIdMacOs()
    {
        var detector = new HostDetector(
            PlatformID.MacOSX,
            () => "macos-machine-id",
            () => throw new Exception("should not be called"));
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Equal("macos-machine-id", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

    [Fact]
    public void TestHostMachineIdWindows()
    {
        var detector = new HostDetector(
            PlatformID.Win32NT,
            () => throw new Exception("should not be called"),
            () => "windows-machine-id");
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Equal("windows-machine-id", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }
}
