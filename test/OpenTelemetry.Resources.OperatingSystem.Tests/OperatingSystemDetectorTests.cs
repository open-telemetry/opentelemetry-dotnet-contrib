// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using Xunit;

namespace OpenTelemetry.Resources.OperatingSystem.Test;

public class OperatingSystemDetectorTests
{
    [Fact]
    public void TestOperatingSystemAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddOperatingSystemDetector().Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        string expectedPlatform;
        string expectedDescription;
        string expectedName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            expectedPlatform = OperatingSystemSemanticConventions.OperatingSystemsValues.Windows;
            expectedDescription = "Windows";
            expectedName = "Windows";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            expectedPlatform = OperatingSystemSemanticConventions.OperatingSystemsValues.Linux;
            expectedDescription = "Linux";
            expectedName = "Linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            expectedPlatform = OperatingSystemSemanticConventions.OperatingSystemsValues.Darwin;
            expectedDescription = "Darwin";
            expectedName = "MacOS";
        }
        else
        {
            throw new PlatformNotSupportedException("Unknown platform");
        }

        Assert.Equal(5, resourceAttributes.Count);
        Assert.True(resourceAttributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemDescription));
        Assert.True(resourceAttributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemName));
        Assert.True(resourceAttributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemType));
        Assert.True(resourceAttributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId));
        Assert.True(resourceAttributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemVersion));

        if (expectedName != "Linux")
        {
            Assert.Contains(expectedDescription, resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemDescription]);
            Assert.Equal(expectedName, resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        }

        Assert.Equal(expectedPlatform, resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }
}
