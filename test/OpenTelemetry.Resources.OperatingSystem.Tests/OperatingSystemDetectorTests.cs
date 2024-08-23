// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Xml.Linq;
#endif
using System.Runtime.InteropServices;
using Xunit;

namespace OpenTelemetry.Resources.OperatingSystem.Test;

public class OperatingSystemDetectorTests
{
#if NET
    public const string MacOSPlist = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                                        <!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
                                        <plist version=""1.0"">
                                        <dict>
                                            <key>ProductBuildVersion</key>
                                            <string>10K549</string>
                                            <key>ProductCopyright</key>
                                            <string>1983-2011 Apple Inc.</string>
                                            <key>ProductName</key>
                                            <string>Mac OS X</string>
                                            <key>ProductUserVisibleVersion</key>
                                            <string>10.6.8</string>
                                            <key>ProductVersion</key>
                                            <string>10.6.8</string>
                                        </dict>
                                        </plist>";

    public const string LinuxOsRelease = @"NAME=Ubuntu
                                            VERSION=""22.04 LTS (Jammy Jellyfish)""
                                            VERSION_ID=""22.04""
                                            VERSION_CODENAME=jammy
                                            ID=ubuntu
                                            HOME_URL=https://www.ubuntu.com/
                                            SUPPORT_URL=https://help.ubuntu.com/
                                            BUG_REPORT_URL=https://bugs.launchpad.net/ubuntu
                                            PRIVACY_POLICY_URL=https://www.ubuntu.com/legal/terms-and-policies/privacy-policy
                                            UBUNTU_CODENAME=jammy
                                            BUILD_ID=20240823";
#endif

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
        XElement dict = XElement.Parse(MacOSPlist).Element("dict")!;

        string? name = OperatingSystemDetector.GetPlistValue(dict, "ProductName");
        string? version = OperatingSystemDetector.GetPlistValue(dict, "ProductVersion");
        string? buildId = OperatingSystemDetector.GetPlistValue(dict, "ProductBuildVersion");

        Assert.Equal("Mac OS X", name);
        Assert.Equal("10.6.8", version);
        Assert.Equal("10K549", buildId);
    }

    [Fact]
    public void TestParseLinuxOsRelease()
    {
        string[] osReleaseContent = LinuxOsRelease
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        string? name = OperatingSystemDetector.GetOsReleaseValue(osReleaseContent, "NAME=") ?? "Linux";
        string? version = OperatingSystemDetector.GetOsReleaseValue(osReleaseContent, "VERSION_ID=");
        string? buildId = OperatingSystemDetector.GetOsReleaseValue(osReleaseContent, "BUILD_ID=");

        Assert.Equal("Ubuntu", name);
        Assert.Equal("22.04", version);
        Assert.Equal("20240823", buildId);
    }

#endif
}
