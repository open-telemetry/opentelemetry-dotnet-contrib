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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            expectedPlatform = OperatingSystemSemanticConventions.OperatingSystemsValues.Windows;
            expectedDescription = "Windows";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            expectedPlatform = OperatingSystemSemanticConventions.OperatingSystemsValues.Linux;
            expectedDescription = "Linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            expectedPlatform = OperatingSystemSemanticConventions.OperatingSystemsValues.Darwin;
            expectedDescription = "Darwin";
        }
        else
        {
            throw new PlatformNotSupportedException("Unknown platform");
        }

        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemDescription, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemName, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemType, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemVersion, resourceAttributes.Keys);

        // Not checking on Linux because the description may vary depending on the distribution.
        if (expectedDescription != "Linux")
        {
            Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId, resourceAttributes.Keys);
            Assert.Contains(expectedDescription, resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemDescription]);
            Assert.Equal(5, resourceAttributes.Count);
        }

        Assert.Equal(expectedPlatform, resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }
}
