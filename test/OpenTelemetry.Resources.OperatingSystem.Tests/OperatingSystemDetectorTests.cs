// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Resources.OperatingSystem.Tests;

public class OperatingSystemDetectorTests
{
    [Fact]
    public void TestOperatingSystemAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddOperatingSystemDetector().Build();

        Assert.NotNull(resource);
        Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

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
#if NET
            expectedDescription = "macOS";
#else
            expectedDescription = "Darwin";
#endif
        }
        else
        {
            throw new PlatformNotSupportedException("Unknown platform");
        }

        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemDescription, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemName, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemType, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemVersion, resourceAttributes.Keys);
        Assert.Contains(OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId, resourceAttributes.Keys);

        // Not checking on Linux because the description may vary depending on the distribution.
        if (expectedDescription != "Linux")
        {
            Assert.Contains(expectedDescription, resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemDescription]);
            Assert.Equal(5, resourceAttributes.Count);
        }

        Assert.Equal(expectedPlatform, resourceAttributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }

    [Fact]
    public void TestDetectWithUnknownPlatformReturnsEmptyResource()
    {
        var resource = CreateDetector(null, null).Detect();

        Assert.NotNull(resource);
        Assert.Empty(resource.Attributes);
    }

    [Fact]
    public void TestDetectWithUnsupportedPlatformReturnsOnlyTypeAndDescription()
    {
        var resource = CreateDetector("unsupported", null).Detect();

        var attributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal(2, attributes.Count);
        Assert.Equal("unsupported", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
        Assert.True(attributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemDescription));
    }

    [Fact]
    public void TestDetectWindowsWithMissingRegistryKeyReturnsOnlyTypeAndDescription()
    {
        // On Windows the key does not exist, elsewhere the registry is not supported.
        // Either way the detector should fall back to the basic attributes.
        var resource = CreateDetector(OperatingSystemSemanticConventions.OperatingSystemsValues.Windows, "OpenTelemetryDoesNotExist").Detect();

        var attributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal(2, attributes.Count);
        Assert.Equal(
            OperatingSystemSemanticConventions.OperatingSystemsValues.Windows,
            attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }

#if NET
    [Fact]
    public void TestParseMacOSPlist()
    {
        var path = "Samples/SystemVersion.plist";
        var osDetector = new OperatingSystemDetector(
            OperatingSystemSemanticConventions.OperatingSystemsValues.Darwin,
            null,
            null,
            null,
            [path]);

        var resource = osDetector.Detect();

        Assert.NotNull(resource);
        Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

        var attributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal("Mac OS X", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        Assert.Equal("10.6.8", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemVersion]);
        Assert.Equal("10K549", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId]);
    }

    [Fact]
    public void TestParseLinuxOsRelease()
    {
        var path = "Samples/os-release";
        var kernelPath = "Samples/kernelOsrelease";
        var osDetector = new OperatingSystemDetector(
            OperatingSystemSemanticConventions.OperatingSystemsValues.Linux,
            null,
            kernelPath,
            [path],
            null);

        var resource = osDetector.Detect();

        Assert.NotNull(resource);
        Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

        var attributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

        Assert.Equal("Ubuntu", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
        Assert.Equal("22.04", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemVersion]);
        Assert.Equal("5.15.0-76-generic", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId]);
    }

    [Fact]
    public void TestParseLinuxOsReleaseWithQuotedValuesAndBuildId()
    {
        WithTempFile(
            ["NAME='Test OS'", "BUILD_ID=\"2024.01\"", "VERSION_ID=1.2"],
            path =>
            {
                var attributes = DetectLinux(path, null);

                Assert.Equal(5, attributes.Count);
                Assert.Equal("Test OS", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
                Assert.Equal("2024.01", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId]);
                Assert.Equal("1.2", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemVersion]);
            });
    }

    [Fact]
    public void TestParseLinuxOsReleaseWithoutNameOrVersionUsesDefaults()
    {
        WithTempFile(
            ["ID=test"],
            path =>
            {
                var attributes = DetectLinux(path, "Samples/kernelOsrelease");

                Assert.Equal(4, attributes.Count);
                Assert.Equal("Linux", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemName]);
                Assert.Equal("5.15.0-76-generic", attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemBuildId]);
                Assert.False(attributes.ContainsKey(OperatingSystemSemanticConventions.AttributeOperatingSystemVersion));
            });
    }

    [Fact]
    public void TestParseLinuxOsReleaseWithMissingKernelReleaseReturnsOnlyTypeAndDescription()
    {
        WithTempFile(
            ["NAME=Test"],
            path =>
            {
                var attributes = DetectLinux(path, "Samples/does-not-exist");

                Assert.Equal(2, attributes.Count);
            });
    }

    [Fact]
    public void TestParseLinuxOsReleaseWithMissingFileReturnsOnlyTypeAndDescription()
    {
        var attributes = DetectLinux("Samples/does-not-exist", "Samples/kernelOsrelease");

        Assert.Equal(2, attributes.Count);
        Assert.Equal(
            OperatingSystemSemanticConventions.OperatingSystemsValues.Linux,
            attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }

    [Fact]
    public void TestParseMacOSPlistWithMissingFileReturnsOnlyTypeAndDescription()
    {
        var attributes = DetectMacOS("Samples/does-not-exist");

        Assert.Equal(2, attributes.Count);
        Assert.Equal(
            OperatingSystemSemanticConventions.OperatingSystemsValues.Darwin,
            attributes[OperatingSystemSemanticConventions.AttributeOperatingSystemType]);
    }

    [Fact]
    public void TestParseMacOSPlistWithMismatchedKeysAndValuesReturnsOnlyTypeAndDescription()
    {
        WithTempFile(
            ["<plist version=\"1.0\">", "<dict>", "<key>ProductName</key>", "<key>ProductVersion</key>", "<string>Mac OS X</string>", "</dict>", "</plist>"],
            path => Assert.Equal(2, DetectMacOS(path).Count));
    }

    [Fact]
    public void TestParseMacOSPlistWithoutDictReturnsOnlyTypeAndDescription()
    {
        WithTempFile(
            ["<plist version=\"1.0\">", "</plist>"],
            path => Assert.Equal(2, DetectMacOS(path).Count));
    }

    [Fact]
    public void TestParseMacOSPlistWithInvalidXmlReturnsOnlyTypeAndDescription()
    {
        WithTempFile(
            ["this is not xml"],
            path => Assert.Equal(2, DetectMacOS(path).Count));
    }

    private static Dictionary<string, string> DetectLinux(string osReleasePath, string? kernelReleasePath)
    {
        var detector = new OperatingSystemDetector(
            OperatingSystemSemanticConventions.OperatingSystemsValues.Linux,
            null,
            kernelReleasePath,
            [osReleasePath],
            null);

        return detector.Detect().Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
    }

    private static Dictionary<string, string> DetectMacOS(string plistPath)
    {
        var detector = new OperatingSystemDetector(
            OperatingSystemSemanticConventions.OperatingSystemsValues.Darwin,
            null,
            null,
            null,
            [plistPath]);

        return detector.Detect().Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
    }

    private static void WithTempFile(string[] lines, Action<string> test)
    {
        var path = Path.GetTempFileName();

        try
        {
            File.WriteAllLines(path, lines);
            test(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
#endif

    private static OperatingSystemDetector CreateDetector(string? osType, string? registryKey) =>
#if NET
        new(osType, registryKey, null, null, null);
#else
        new(osType, registryKey);
#endif
}
