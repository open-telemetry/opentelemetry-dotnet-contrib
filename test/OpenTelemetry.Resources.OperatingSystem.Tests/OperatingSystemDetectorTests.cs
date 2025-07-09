// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Resources.OperatingSystem.Test;

public class OperatingSystemDetectorTests
{
    [Fact]
    public void TestOperatingSystemAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddOperatingSystemDetector().Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        string expectedPlatform;
        bool windows = true
#if NET
            && System.OperatingSystem.IsWindows()
#endif
            ;

        if (windows)
        {
            expectedPlatform = "windows32nt";
        }
#if NET
        else if (System.OperatingSystem.IsLinux())
        {
            expectedPlatform = "unix";
        }
        else if (System.OperatingSystem.IsMacOS())
        {
            expectedPlatform = "unix";
        }
#endif
        else
        {
            throw new PlatformNotSupportedException("Unknown platform");
        }

        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemDescription, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemName, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemType, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemVersion, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId, resourceAttributes.Keys);

        Assert.Equal(expectedPlatform, resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }

#if NET
    [Fact]
    public void TestParseMacOSPlist()
    {
        var path = "Samples/SystemVersion.plist";
        var osDetector = new OperatingSystemDetector(
            OperatingSystemCategory.MacOS,
            null,
            null,
            null,
            [path]);
        var attributes = osDetector.Detect().Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal("Mac OS X", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        Assert.IsType<string[]>(attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemFamily]);
        Assert.Contains("darwin", (string[])attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemFamily]);
        Assert.Equal("10.6.8", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemVersion]);
        Assert.Equal("10K549", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId]);
    }

    [Fact]
    public void TestParseLinuxOsRelease()
    {
        var path = "Samples/os-release";
        var kernelPath = "Samples/kernelOsrelease";
        var osDetector = new OperatingSystemDetector(
            OperatingSystemCategory.Linux,
            null,
            kernelPath,
            [path],
            null);
        var attributes = osDetector.Detect().Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal("Ubuntu", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        Assert.Equal("Ubuntu 22.04 LTS (Jammy Jellyfish)", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemDescription]);
        Assert.IsType<string[]>(attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemFamily]);
        Assert.Contains("ubuntu", (string[])attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemFamily]);
        Assert.Equal("unix", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
        Assert.Equal("22.04", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemVersion]);
        Assert.Equal("5.15.0-76-generic", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId]);
    }

    [Fact]
    public void TestParseAppleOsRelease()
    {
        var osDetector = new OperatingSystemDetector(
            OperatingSystemCategory.AppleOS,
            null,
            null,
            null,
            null);
        var attributes = osDetector.Detect().Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal("UNKNOWN", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        Assert.IsType<string[]>(attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemFamily]);
        Assert.Contains("darwin", (string[])attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemFamily]);
        Assert.Equal("unix", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }

    [Fact]
    public void TestParseAndroidRelease()
    {
        var osDetector = new OperatingSystemDetector(
            OperatingSystemCategory.Android,
            null,
            null,
            null,
            null);
        var attributes = osDetector.Detect().Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal("Android", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        Assert.IsType<string[]>(attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemFamily]);
        Assert.Contains("aosp", (string[])attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemFamily]);
        Assert.Equal("unix", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }

    [Fact]
    public void TestParseNullCategory()
    {
        var osDetector = new OperatingSystemDetector(
            null,
            null,
            null,
            null,
            null);
        var attributes = osDetector.Detect().Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Empty(attributes);
    }
#endif
}
