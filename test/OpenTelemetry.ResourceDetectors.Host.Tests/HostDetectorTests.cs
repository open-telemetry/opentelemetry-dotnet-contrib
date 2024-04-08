// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.Host.Tests;

public class HostDetectorTests
{
    private static readonly IEnumerable<string> ETCMACHINEID = new[] { "Samples/etc_machineid" };
    private static readonly IEnumerable<string> ETCVARDBUSMACHINEID = new[] { "Samples/etc_var_dbus_machineid" };

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
    public void TestHostMachineIdLinux()
    {
        var combos = new[]
        {
            (Enumerable.Empty<string>(), string.Empty),
            (ETCMACHINEID, "etc_machineid"),
            (ETCVARDBUSMACHINEID, "etc_var_dbus_machineid"),
            (Enumerable.Concat(ETCMACHINEID, ETCVARDBUSMACHINEID), "etc_machineid"),
        };

        foreach (var (path, expected) in combos)
        {
            var detector = new HostDetector(
                PlatformID.Unix,
                () => path,
                () => throw new Exception("should not be called"),
                () => throw new Exception("should not be called"));
            var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
            var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

            if (string.IsNullOrEmpty(expected))
            {
                Assert.Empty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
            }
            else
            {
                Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
                Assert.Equal(expected, resourceAttributes[HostSemanticConventions.AttributeHostId]);
            }
        }
    }

    [Fact]
    public void TestHostMachineIdMacOs()
    {
        var detector = new HostDetector(
            PlatformID.MacOSX,
            () => Enumerable.Empty<string>(),
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
            () => Enumerable.Empty<string>(),
            () => throw new Exception("should not be called"),
            () => "windows-machine-id");
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Equal("windows-machine-id", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }
}
