// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace OpenTelemetry.Resources.Host.Tests;

public class HostDetectorTests
{
    private const string EnableNetworkAddressesEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_NETWORK_ADDRESSES";
    private const string EnableCpuInfoEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_CPU_INFO";

    private const string WindowsCpuIdentifier = "AMD64 Family 25 Model 1 Stepping 1";

    private const string LinuxCpuInfoOutput =
        "processor\t: 0\n" +
        "vendor_id\t: AuthenticAMD\n" +
        "cpu family\t: 25\n" +
        "model\t\t: 17\n" +
        "model name\t: AMD EPYC 9V74 80-Core Processor\n" +
        "stepping\t: 1\n" +
        "cache size\t: 1024 KB\n" +
        "\n" +
        "processor\t: 1\n" +
        "vendor_id\t: AuthenticAMD\n" +
        "cpu family\t: 25\n" +
        "model\t\t: 99\n" +
        "model name\t: AMD EPYC 9V74 80-Core Processor\n" +
        "stepping\t: 9\n" +
        "cache size\t: 1024 KB\n";

    private const string Arm64CpuInfoOutput =
        "processor\t: 0\n" +
        "BogoMIPS\t: 48.00\n" +
        "Features\t: fp asimd evtstrm aes pmull sha1 sha2 crc32\n" +
        "CPU implementer\t: 0x41\n" +
        "CPU architecture: 8\n" +
        "CPU variant\t: 0x0\n" +
        "CPU part\t: 0xd0c\n" +
        "CPU revision\t: 1\n";

    private const string SysctlOutput =
        "machdep.cpu.brand_string: Apple M5\n" +
        "hw.l2cachesize: 6291456\n";

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
        using var networkAddressesEnvironment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, null);
        using var cpuInfoEnvironment = EnvironmentVariableScope.Create(EnableCpuInfoEnvVarName, null);

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

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void TestHostCpuInfoEnabled(string value)
    {
        using var cpuInfoEnvironment = EnvironmentVariableScope.Create(EnableCpuInfoEnvVarName, value);
        using var networkAddressesEnvironment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, null);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.True(resourceAttributes.ContainsKey("host.cpu.model.name"), "host.cpu.model.name should be detected when the flag is set; it is the only host.cpu.* attribute whose source exists on every platform this suite runs on.");
        Assert.NotEmpty(Assert.IsType<string>(resourceAttributes["host.cpu.model.name"]));
        Assert.False(resourceAttributes.ContainsKey("host.ip"), "host.ip should not be detected when only the CPU flag is set.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("false")]
    [InlineData("1")]
    public void TestHostCpuInfoNotEnabled(string? value)
    {
        using var cpuInfoEnvironment = EnvironmentVariableScope.Create(EnableCpuInfoEnvVarName, value);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        Assert.False(resource.Attributes.Any(x => x.Key.StartsWith("host.cpu.", StringComparison.Ordinal)), "No host.cpu.* attribute should be detected when the flag is not set.");
    }

    [Theory]
    [InlineData("AuthenticAMD", "AuthenticAMD")]
    [InlineData("AMD EPYC 7763 64-Core Processor                ", "AMD EPYC 7763 64-Core Processor")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TestNormalizeCpuValue(string? value, string? expected) =>
        Assert.Equal(expected, HostDetector.NormalizeCpuValue(value));

    [Theory]
    [InlineData(LinuxCpuInfoOutput, "vendor_id", "AuthenticAMD")]
    [InlineData(LinuxCpuInfoOutput, "cpu family", "25")]
    [InlineData(LinuxCpuInfoOutput, "model", "17")]
    [InlineData(LinuxCpuInfoOutput, "model name", "AMD EPYC 9V74 80-Core Processor")]
    [InlineData(LinuxCpuInfoOutput, "stepping", "1")]
    [InlineData(LinuxCpuInfoOutput, "cache size", "1024 KB")]
    [InlineData(LinuxCpuInfoOutput, "flags", null)]
    [InlineData(Arm64CpuInfoOutput, "model name", null)]
    [InlineData(null, "vendor_id", null)]
    public void TestParseCpuInfoField(string? cpuInfo, string fieldName, string? expected) =>
        Assert.Equal(expected, HostDetector.ParseCpuInfoField(cpuInfo, fieldName));

    [Theory]
    [InlineData(WindowsCpuIdentifier, "Family", "25")]
    [InlineData(WindowsCpuIdentifier, "Model", "1")]
    [InlineData(WindowsCpuIdentifier, "Stepping", "1")]
    [InlineData(WindowsCpuIdentifier, "Vendor", null)]
    [InlineData("AMD64 Family", "Family", null)]
    [InlineData(null, "Family", null)]
    public void TestParseCpuIdentifierToken(string? identifier, string keyword, string? expected) =>
        Assert.Equal(expected, HostDetector.ParseCpuIdentifierToken(identifier, keyword));

    [Theory]
    [InlineData(SysctlOutput, "machdep.cpu.brand_string", "Apple M5")]
    [InlineData(SysctlOutput, "hw.l2cachesize", "6291456")]
    [InlineData(SysctlOutput, "machdep.cpu.vendor", null)]
    [InlineData(SysctlOutput, "machdep.cpu.brand", null)]
    [InlineData(null, "hw.l2cachesize", null)]
    public void TestParseSysctlField(string? output, string key, string? expected) =>
        Assert.Equal(expected, HostDetector.ParseSysctlField(output, key));

    [Theory]
    [InlineData("1024 KB", 1048576)]
    [InlineData("6144 KB", 6291456)]
    [InlineData("6291456", 6291456)]
    [InlineData("1024K", 1048576)]
    [InlineData("unknown", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void TestParseCacheSize(string? value, int? expected) =>
        Assert.Equal(expected, HostDetector.ParseCacheSize(value));

    [Fact]
    public void TestSystemLogicalProcessorInformationCacheLayout()
    {
        // The union's ULONGLONG Reserved[2] aligns it to 8, so Relationship is followed by padding.
        var dataOffset = IntPtr.Size == 8 ? 16 : 8;
        var entrySize = dataOffset + 16;
        var buffer = Marshal.AllocHGlobal(entrySize);

        try
        {
            for (var i = 0; i < entrySize; i++)
            {
                Marshal.WriteByte(buffer, i, 0);
            }

            Marshal.WriteInt32(buffer, IntPtr.Size, 2);
            Marshal.WriteByte(buffer, dataOffset, 2);
            Marshal.WriteByte(buffer, dataOffset + 1, 8);
            Marshal.WriteInt16(buffer, dataOffset + 2, 64);
            Marshal.WriteInt32(buffer, dataOffset + 4, 524288);

            var entry = Marshal.PtrToStructure<HostDetector.NativeMethods.SystemLogicalProcessorInformation>(buffer);

            Assert.Equal(HostDetector.NativeMethods.RELATIONCACHE, entry.Relationship);
            Assert.Equal((byte)2, entry.Data.Cache.Level);
            Assert.Equal(524288u, entry.Data.Cache.Size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
