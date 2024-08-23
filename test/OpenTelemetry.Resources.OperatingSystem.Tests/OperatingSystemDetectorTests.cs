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

#if NET
    [Fact]
    public void TestParseMacOSPlist()
    {
        List<KeyValuePair<string, object>> resourceAttributes = new();
        string baseDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));
        string path = Path.Combine(baseDirectory, "Samples", "SystemVersion.plist");
        OperatingSystemDetector.AddMacOSAttributes(
            resourceAttributes,
            [path]);
        var attributes = resourceAttributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal("Mac OS X", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        Assert.Equal("10.6.8", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemVersion]);
        Assert.Equal("10K549", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId]);
    }

    [Fact]
    public void TestParseLinuxOsRelease()
    {
        List<KeyValuePair<string, object>> resourceAttributes = new();
        string baseDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));
        string path = Path.Combine(baseDirectory, "Samples", "os-release");
        OperatingSystemDetector.AddLinuxAttributes(resourceAttributes, path);
        var attributes = resourceAttributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal("Ubuntu", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        Assert.Equal("22.04", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemVersion]);
        Assert.Equal("20240823", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId]);
    }
#endif
}
