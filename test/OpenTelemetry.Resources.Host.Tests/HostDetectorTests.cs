// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.NetworkInformation;
#if NET
using System.Runtime.InteropServices;
#endif

namespace OpenTelemetry.Resources.Host.Tests;

public class HostDetectorTests
{
    private const string EnableNetworkAddressesEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_NETWORK_ADDRESSES";

#if !NETFRAMEWORK
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
#endif

#if NET
    private static readonly IEnumerable<string> ETCMACHINEID = ["Samples/etc_machineid"];
    private static readonly IEnumerable<string> ETCVARDBUSMACHINEID = ["Samples/etc_var_dbus_machineid"];
#endif

    [Fact]
    public void TestHostAttributes()
    {
        using var environment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, null);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        Assert.NotNull(resource);
        Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);

#if NET
        Assert.Equal(3, resourceAttributes.Count);
#else
        Assert.Equal(2, resourceAttributes.Count);
#endif

        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostName]);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
#if NET
#pragma warning disable IDE0072 // Add missing cases
        var expectedArch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm => "arm32",
#if NET
            Architecture.Armv6 => "arm32",
            Architecture.LoongArch64 => null,
#if NET10_0_OR_GREATER
            Architecture.RiscV64 => null,
#endif
            Architecture.Ppc64le => "ppc64",
            Architecture.Wasm => null,
#endif
            Architecture.X64 => "amd64",
#pragma warning disable CA1308 // Normalize strings to uppercase
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
#pragma warning restore CA1308 // Normalize strings to uppercase
        };
#pragma warning restore IDE0072 // Add missing cases

        if (expectedArch is not null)
        {
            Assert.NotEmpty(resourceAttributes["host.arch"]);
            Assert.Equal(expectedArch, resourceAttributes["host.arch"]);
        }
        else
        {
            Assert.False(resourceAttributes.ContainsKey("host.arch"));
        }
#endif
    }

#if NET
    [Fact]
    public void TestHostMachineIdLinux()
    {
        var combos = new[]
        {
            ([], null),
            (ETCMACHINEID, "etc_machineid"),
            (ETCVARDBUSMACHINEID, "etc_var_dbus_machineid"),
            (Enumerable.Concat(ETCMACHINEID, ETCVARDBUSMACHINEID), "etc_machineid"),
        };

        foreach (var (path, expected) in combos)
        {
            var detector = new HostDetector(
                osPlatform => osPlatform == OSPlatform.Linux,
                () => path,
                () => throw new Exception("should not be called"),
                () => throw new Exception("should not be called"));
            var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();

            Assert.NotNull(resource);
            Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

            var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
            if (string.IsNullOrEmpty(expected))
            {
                Assert.False(resourceAttributes.ContainsKey(HostSemanticConventions.AttributeHostId));
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
            osPlatform => osPlatform == OSPlatform.OSX,
            () => [],
            () => MacOSMachineIdOutput,
            () => throw new Exception("should not be called"));
        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();

        Assert.NotNull(resource);
        Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
        Assert.Equal("1AB2345C-03E4-57D4-A375-1234D48DE123", resourceAttributes[HostSemanticConventions.AttributeHostId]);
    }

    [Fact]
    public void TestParseMacOsOutput()
    {
        var id = HostDetector.ParseMacOsOutput(MacOSMachineIdOutput);
        Assert.Equal("1AB2345C-03E4-57D4-A375-1234D48DE123", id);
    }
#endif

    [Fact]
    public void TestHostMachineIdWindows()
    {
#if NET
        var detector = new HostDetector(osPlatform => osPlatform == OSPlatform.Windows, () => [], () => throw new Exception("should not be called"), () => "windows-machine-id");
#else
        var detector = new HostDetector(() => "windows-machine-id");
#endif

        var resource = ResourceBuilder.CreateEmpty().AddDetector(detector).Build();

        Assert.NotNull(resource);
        Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => (string)x.Value);
        Assert.NotEmpty(resourceAttributes[HostSemanticConventions.AttributeHostId]);
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
        });

        var resource = detector.Detect();

        Assert.NotNull(resource);
        Assert.StartsWith("https://opentelemetry.io/schemas/", resource.SchemaUrl);

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

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void TestHostNetworkAddressesEnabled(string value)
    {
        using var environment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, value);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.True(resourceAttributes.ContainsKey("host.ip"), "host.ip should be detected when the flag is set and the host has an eligible network interface.");
        Assert.True(resourceAttributes.ContainsKey("host.mac"), "host.mac should be detected when the flag is set and the host has an eligible network interface.");

        Assert.NotEmpty(Assert.IsType<string[]>(resourceAttributes["host.ip"]));
        Assert.NotEmpty(Assert.IsType<string[]>(resourceAttributes["host.mac"]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("false")]
    [InlineData("1")]
    public void TestHostNetworkAddressesNotEnabled(string? value)
    {
        using var environment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, value);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.False(resourceAttributes.ContainsKey("host.ip"), "host.ip should not be detected when the flag is not set.");
        Assert.False(resourceAttributes.ContainsKey("host.mac"), "host.mac should not be detected when the flag is not set.");
    }

    [Theory]
    [InlineData(OperationalStatus.Up, NetworkInterfaceType.Ethernet, true)]
    [InlineData(OperationalStatus.Up, NetworkInterfaceType.Wireless80211, true)]
    [InlineData(OperationalStatus.Up, NetworkInterfaceType.Unknown, true)]
    [InlineData(OperationalStatus.Up, NetworkInterfaceType.Tunnel, true)]
    [InlineData(OperationalStatus.Up, NetworkInterfaceType.Loopback, false)]
    [InlineData(OperationalStatus.Down, NetworkInterfaceType.Ethernet, false)]
    [InlineData(OperationalStatus.Dormant, NetworkInterfaceType.Ethernet, false)]
    public void TestShouldIncludeNetworkInterface(OperationalStatus operationalStatus, NetworkInterfaceType networkInterfaceType, bool expected) =>
        Assert.Equal(expected, HostDetector.ShouldIncludeNetworkInterface(operationalStatus, networkInterfaceType));

    [Theory]
    [InlineData("192.0.2.1", true)]
    [InlineData("2001:db8::1", true)]
    [InlineData("10.0.0.4", true)]
    [InlineData("169.255.0.1", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("169.254.0.1", false)]
    [InlineData("fe80::1", false)]
    public void TestShouldIncludeIpAddress(string address, bool expected) =>
        Assert.Equal(expected, HostDetector.ShouldIncludeIpAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }, "AA-BB-CC-DD-EE-FF")]
    [InlineData(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0xFF }, "00-11-22-33-44-FF")]
    [InlineData(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11 }, "AA-BB-CC-DD-EE-FF-00-11")]
    public void TestFormatPhysicalAddress(byte[] address, string expected) =>
        Assert.Equal(expected, HostDetector.FormatPhysicalAddress(new PhysicalAddress(address)));

    [Fact]
    public void TestFormatPhysicalAddressWithEmptyAddress() =>
        Assert.Null(HostDetector.FormatPhysicalAddress(PhysicalAddress.None));
}
