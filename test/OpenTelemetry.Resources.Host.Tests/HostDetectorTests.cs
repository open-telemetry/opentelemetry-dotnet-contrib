// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Runtime.InteropServices;
#endif
using Xunit;

namespace OpenTelemetry.Resources.Host.Tests;

public class HostDetectorTests
{
    private const string MacOSMachineIdOutput = @"+-o J293AP  <class IOPlatformExpertDevice, id 0x100000227, registered, matched,$
        {
          ""IOPolledInterface"" = ""AppleARMWatchdogTimerHibernateHandler is not seria$
          ""#address-cells"" = <02000000>
          ""AAPL,phandle"" = <01000000>
          ""serial-number"" = <432123465233514651303544000000000000000000000000000000$
          ""IOBusyInterest"" = ""IOCommand is not serializable""
          ""target-type"" = <""J293"">
          ""platform-name"" = <743831303300000000000000000000000000000000000000000000$
          ""secure-root-prefix"" = <""md"">
          ""name"" = <""device-tree"">
          ""region-info"" = <4c4c2f41000000000000000000000000000000000000000000000000$
          ""manufacturer"" = <""Apple Inc."">
          ""compatible"" = <""J293AP"",""MacBookPro17,1"",""AppleARM"">
          ""config-number"" = <000000000000000000000000000000000000000000000000000000$
          ""IOPlatformSerialNumber"" = ""A01BC3QFQ05D""
          ""regulatory-model-number"" = <41323333380000000000000000000000000000000000$
          ""time-stamp"" = <""Mon Jun 27 20:12:10 PDT 2022"">
          ""clock-frequency"" = <00366e01>
          ""model"" = <""MacBookPro17,1"">
          ""mlb-serial-number"" = <432123413230363030455151384c4c314a0000000000000000$
          ""model-number"" = <4d59443832000000000000000000000000000000000000000000000$
          ""IONWInterrupts"" = ""IONWInterrupts""
          ""model-config"" = <""SUNWAY;MoPED=0x803914B08BE6C5AF0E6C990D7D8240DA4CAC2FF$
          ""device_type"" = <""bootrom"">
          ""#size-cells"" = <02000000>
          ""IOPlatformUUID"" = ""1AB2345C-03E4-57D4-A375-1234D48DE123""
        }";

#if NET
    private const string ETCMACHINEID = "Samples/etc_machineid";
    private const string ETCVARDBUSMACHINEID = "Samples/etc_var_dbus_machineid";
#endif

    [Fact]
    public void TestHostAttributes()
    {
        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(2, resourceAttributes.Count);

        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostIp]);
    }

    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 3)]
    [InlineData(true, false, 3)]
    [InlineData(true, true, 4)]
    public void TestHostAttributesOptions(bool ip, bool mac, int attributes)
    {
        var resource = ResourceBuilder.CreateEmpty().AddHostDetector(new HostDetectorOptions()
        {
            IncludeIP = ip,
            IncludeMac = mac,
        }).Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(attributes, resourceAttributes.Count);

        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostId]);
        if (mac)
        {
            Assert.IsType<string[]>(resourceAttributes[HostSemanticConventions.AttributeHostMac]);
            Assert.NotEmpty((string[])resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        }

        if (ip)
        {
            Assert.IsType<string[]>(resourceAttributes[HostSemanticConventions.AttributeHostIp]);
            Assert.NotEmpty((string[])resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        }
    }

#if NET
    [Theory]
    [InlineData(new string[] { }, null)]
    [InlineData(new[] { ETCMACHINEID }, "etc_machineid")]
    [InlineData(new[] { ETCVARDBUSMACHINEID }, "etc_var_dbus_machineid")]
    [InlineData(new[] { ETCMACHINEID, ETCVARDBUSMACHINEID }, "etc_machineid")]
    public void TestHostMachineIdLinux(IEnumerable<string> path, string? expected)
    {
        var detector = new HostDetector(
            osPlatform => osPlatform == OSPlatform.Linux,
            () => path,
            () => throw new Exception("should not be called"),
            () => throw new Exception("should not be called"),
            new HostDetectorOptions());
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        if (string.IsNullOrEmpty(expected))
        {
            Assert.False(resourceAttributes.ContainsKey(HostSemanticConventions.AttributeHostId));
        }
        else
        {
            Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostId]);
            Assert.Equal(expected, resourceAttributes[HostSemanticConventions.AttributeHostId]);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TestHostMachineIdLinuxOptions(bool ip, bool mac)
    {
        var detector = new HostDetector(
            osPlatform => osPlatform == OSPlatform.Linux,
            () => new[] { ETCMACHINEID },
            () => throw new Exception("should not be called"),
            () => throw new Exception("should not be called"),
            new HostDetectorOptions()
            {
                IncludeIP = ip,
                IncludeMac = mac,
            });
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostName]);
        if (mac)
        {
            Assert.IsType<string[]>(resourceAttributes[HostSemanticConventions.AttributeHostMac]);
            Assert.NotEmpty((string[])resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        }

        if (ip)
        {
            Assert.IsType<string[]>(resourceAttributes[HostSemanticConventions.AttributeHostIp]);
            Assert.NotEmpty((string[])resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        }

        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Equal("etc_machineid", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

    [Fact]
    public void TestHostMachineIdMacOs()
    {
        var detector = new HostDetector(
            osPlatform => osPlatform == OSPlatform.OSX,
            () => [],
            () => MacOSMachineIdOutput,
            () => throw new Exception("should not be called"),
            new HostDetectorOptions());
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        Assert.Equal("1AB2345C-03E4-57D4-A375-1234D48DE123", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TestHostMachineIdMacOsOptions(bool ip, bool mac)
    {
        var detector = new HostDetector(
            osPlatform => osPlatform == OSPlatform.OSX,
            () => [],
            () => MacOSMachineIdOutput,
            () => throw new Exception("should not be called"),
            new HostDetectorOptions()
            {
                IncludeIP = ip,
                IncludeMac = mac,
            });
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostId]);

        if (mac)
        {
            Assert.IsType<string[]>(resourceAttributes[HostSemanticConventions.AttributeHostMac]);
            Assert.NotEmpty((string[])resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        }

        if (ip)
        {
            Assert.IsType<string[]>(resourceAttributes[HostSemanticConventions.AttributeHostIp]);
            Assert.NotEmpty((string[])resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        }

        Assert.Equal("1AB2345C-03E4-57D4-A375-1234D48DE123", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }
#endif

    [Fact]
    public void TestParseMacOsOutput()
    {
        var id = HostDetector.ParseMacOsOutput(MacOSMachineIdOutput);
        Assert.Equal("1AB2345C-03E4-57D4-A375-1234D48DE123", id);
    }

    [Fact]
    public void TestHostMachineIdWindows()
    {
#if NET
        var detector = new HostDetector(
            osPlatform => osPlatform == OSPlatform.Windows,
            () => [],
            () => throw new Exception("should not be called"),
            () => "windows-machine-id",
            new HostDetectorOptions());
#else
        var detector = new HostDetector(
            () => [],
            () => throw new Exception("should not be called"),
            () => "windows-machine-id",
            new HostDetectorOptions());
#endif

        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        Assert.Equal("windows-machine-id", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TestHostMachineIdWindowsOptions(bool ip, bool mac)
    {
#if NET
        var detector = new HostDetector(osPlatform => osPlatform == OSPlatform.Windows, () => [], () => throw new Exception("should not be called"), () => "windows-machine-id", new HostDetectorOptions()
        {
            IncludeIP = ip,
            IncludeMac = mac,
        });
#else
        var detector = new HostDetector(() => [], () => throw new Exception("should not be called"), () => "windows-machine-id", new HostDetectorOptions()
        {
            IncludeIP = ip,
            IncludeMac = mac,
        });
#endif

        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();
        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.IsType<string>(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.NotEmpty((string)resourceAttributes[HostSemanticConventions.AttributeHostId]);

        if (mac)
        {
            Assert.IsType<string[]>(resourceAttributes[HostSemanticConventions.AttributeHostMac]);
            Assert.NotEmpty((string[])resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostMac]);
        }

        if (ip)
        {
            Assert.IsType<string[]>(resourceAttributes[HostSemanticConventions.AttributeHostIp]);
            Assert.NotEmpty((string[])resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        }
        else
        {
            Assert.Throws<KeyNotFoundException>(() => resourceAttributes[HostSemanticConventions.AttributeHostIp]);
        }

        Assert.Equal("windows-machine-id", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

#if NET
    [Fact]
    public void TestPlatformSpecificMethodInvocation()
    {
        var linuxMethodCalled = false;
        var macOsMethodCalled = false;
        var windowsMethodCalled = false;
        var detector = new HostDetector(
            () =>
            {
                linuxMethodCalled = true;
                return [];
            },
            () =>
            {
                macOsMethodCalled = true;
                return string.Empty;
            },
            () =>
            {
                windowsMethodCalled = true;
                return string.Empty;
            },
            new HostDetectorOptions());
        detector.Detect();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.True(linuxMethodCalled, "Linux method should have been called.");
            Assert.False(windowsMethodCalled, "Windows method should not have been called.");
            Assert.False(macOsMethodCalled, "MacOS method should not have been called.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.False(linuxMethodCalled, "Linux method should not have been called.");
            Assert.True(windowsMethodCalled, "Windows method should have been called.");
            Assert.False(macOsMethodCalled, "MacOS method should not have been called.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.False(linuxMethodCalled, "Linux method should not have been called.");
            Assert.False(windowsMethodCalled, "Windows method should not have been called.");
            Assert.True(macOsMethodCalled, "MacOS method should have been called.");
        }
        else
        {
            Assert.Fail("Unexpected platform detected.");
        }
    }
#endif
}
