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
    private const string EnableCpuInfoEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_CPU_INFO";

    private const string WindowsCpuIdentifier = "AMD64 Family 25 Model 1 Stepping 1";

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

    private static readonly string LinuxCpuInfoOutput = string.Join(
        "\n",
        "processor\t: 0",
        "vendor_id\t: AuthenticAMD",
        "cpu family\t: 25",
        "model\t\t: 17",
        "model name\t: AMD EPYC 9V74 80-Core Processor",
        "stepping\t: 1",
        "cache size\t: 1024 KB",
        string.Empty,
        "processor\t: 1",
        "vendor_id\t: AuthenticAMD",
        "cpu family\t: 25",
        "model\t\t: 99",
        "model name\t: AMD EPYC 9V74 80-Core Processor",
        "stepping\t: 9",
        "cache size\t: 1024 KB",
        string.Empty);

    private static readonly string Arm64CpuInfoOutput = string.Join(
        "\n",
        "processor\t: 0",
        "BogoMIPS\t: 48.00",
        "Features\t: fp asimd evtstrm aes pmull sha1 sha2 crc32",
        "CPU implementer\t: 0x41",
        "CPU architecture: 8",
        "CPU variant\t: 0x0",
        "CPU part\t: 0xd0c",
        "CPU revision\t: 1",
        string.Empty);

    private static readonly string SysctlOutput = string.Join(
        "\n",
        "machdep.cpu.brand_string: Apple M5",
        "hw.l2cachesize: 6291456",
        string.Empty);

    private static readonly string IntelSysctlOutput = string.Join(
        "\n",
        "machdep.cpu.vendor: GenuineIntel",
        "machdep.cpu.family: 6",
        "machdep.cpu.model: 6",
        "machdep.cpu.stepping: 1",
        "machdep.cpu.brand_string: 11th Gen Intel(R) Core(TM) i7-1185G7 @ 3.00GHz",
        "hw.l2cachesize: 12288000",
        string.Empty);

#if NET
    private static readonly IEnumerable<string> ETCMACHINEID = ["Samples/etc_machineid"];
    private static readonly IEnumerable<string> ETCVARDBUSMACHINEID = ["Samples/etc_var_dbus_machineid"];
#endif

    public static TheoryData<string?, string, string?> ParseFieldValueProcCpuInfoTestCases() => new()
    {
        { LinuxCpuInfoOutput, "vendor_id", "AuthenticAMD" },
        { LinuxCpuInfoOutput, "cpu family", "25" },
        { LinuxCpuInfoOutput, "model", "17" },
        { LinuxCpuInfoOutput, "model name", "AMD EPYC 9V74 80-Core Processor" },
        { LinuxCpuInfoOutput, "stepping", "1" },
        { LinuxCpuInfoOutput, "cache size", "1024 KB" },
        { LinuxCpuInfoOutput, "flags", null },
        { Arm64CpuInfoOutput, "model name", null },
        { null, "vendor_id", null },
    };

    public static TheoryData<string?, string, string?> ParseFieldValueSysctlTestCases() => new()
    {
        { SysctlOutput, "machdep.cpu.brand_string", "Apple M5" },
        { SysctlOutput, "hw.l2cachesize", "6291456" },
        { SysctlOutput, "machdep.cpu.vendor", null },
        { SysctlOutput, "machdep.cpu.brand", null },
        { IntelSysctlOutput, "machdep.cpu.vendor", "GenuineIntel" },
        { IntelSysctlOutput, "machdep.cpu.family", "6" },
        { IntelSysctlOutput, "machdep.cpu.model", "6" },
        { IntelSysctlOutput, "machdep.cpu.stepping", "1" },
        { IntelSysctlOutput, "machdep.cpu.brand_string", "11th Gen Intel(R) Core(TM) i7-1185G7 @ 3.00GHz" },
        { IntelSysctlOutput, "hw.l2cachesize", "12288000" },
        { null, "hw.l2cachesize", null },
    };

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
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("169.254.0.1", true)]
    [InlineData("fe80::1", true)]
    public void TestShouldIncludeIpAddress(string address, bool expected) =>
        Assert.Equal(expected, HostDetector.ShouldIncludeIpAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData(new[] { "fe80::1%14" }, new[] { "fe80::1" })]
    [InlineData(new[] { "fe80::abc2:4a28:737a:609e%14" }, new[] { "fe80::abc2:4a28:737a:609e" })]
    [InlineData(new[] { "2001:db8::1" }, new[] { "2001:db8::1" })]
    [InlineData(new[] { "192.0.2.1" }, new[] { "192.0.2.1" })]
    [InlineData(new[] { "fe80::1", "192.0.2.1", "fe80::1" }, new[] { "fe80::1", "192.0.2.1" })]
    [InlineData(new[] { "fe80::1%14", "fe80::1%15" }, new[] { "fe80::1" })]
    public void TestFormatIpAddresses(string[] addresses, string[] expected) =>
        Assert.Equal(expected, HostDetector.FormatIpAddresses(addresses.Select(IPAddress.Parse)));

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
    public void TestHostCpuInfoEnabledWindows(string value)
    {
#if NET
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Skipped because current platform is not Windows.");
#endif

        using var cpuInfoEnvironment = EnvironmentVariableScope.Create(EnableCpuInfoEnvVarName, value);
        using var networkAddressesEnvironment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, null);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.True(
            resourceAttributes.ContainsKey("host.cpu.model.name"),
            "host.cpu.model.name should be detected when the flag is set on Windows.");
        Assert.NotEmpty(Assert.IsType<string>(resourceAttributes["host.cpu.model.name"]));
        Assert.True(
            resourceAttributes.ContainsKey("host.cpu.cache.l2.size"),
            "host.cpu.cache.l2.size should be detected on Windows; its source is kernel32, not the registry.");
        Assert.False(resourceAttributes.ContainsKey("host.ip"), "host.ip should not be detected when only the CPU flag is set.");
    }

#if NET
    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void TestHostCpuInfoEnabledLinux(string value)
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() &&
            RuntimeInformation.ProcessArchitecture is not (Architecture.Arm or Architecture.Arm64),
            "Skipped because current platform is not x86 Linux.");

        using var cpuInfoEnvironment = EnvironmentVariableScope.Create(EnableCpuInfoEnvVarName, value);
        using var networkAddressesEnvironment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, null);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.True(
            resourceAttributes.ContainsKey("host.cpu.model.name"),
            "host.cpu.model.name should be detected when the flag is set on x86 Linux.");
        Assert.NotEmpty(Assert.IsType<string>(resourceAttributes["host.cpu.model.name"]));
        Assert.False(resourceAttributes.ContainsKey("host.ip"), "host.ip should not be detected when only the CPU flag is set.");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void TestHostCpuInfoEnabledArmLinux(string value)
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() &&
            RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64,
            "Skipped because current platform is not Arm Linux.");

        using var cpuInfoEnvironment = EnvironmentVariableScope.Create(EnableCpuInfoEnvVarName, value);
        using var networkAddressesEnvironment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, null);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        // Arm /proc/cpuinfo carries CPU implementer, part, architecture and revision instead
        // of the five fields read here; reading those is a follow-up, not a settled omission.
        Assert.False(
            resourceAttributes.ContainsKey("host.cpu.model.name"),
            "host.cpu.model.name has no /proc/cpuinfo source on Arm Linux; reading the Arm fields is a follow-up.");
        Assert.False(resourceAttributes.ContainsKey("host.ip"), "host.ip should not be detected when only the CPU flag is set.");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void TestHostCpuInfoEnabledMacOs(string value)
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Skipped because current platform is not macOS.");

        using var cpuInfoEnvironment = EnvironmentVariableScope.Create(EnableCpuInfoEnvVarName, value);
        using var networkAddressesEnvironment = EnvironmentVariableScope.Create(EnableNetworkAddressesEnvVarName, null);

        var resource = ResourceBuilder.CreateEmpty().AddHostDetector().Build();

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.True(
            resourceAttributes.ContainsKey("host.cpu.model.name"),
            "host.cpu.model.name should be detected when the flag is set on macOS.");
        Assert.NotEmpty(Assert.IsType<string>(resourceAttributes["host.cpu.model.name"]));
        Assert.False(resourceAttributes.ContainsKey("host.ip"), "host.ip should not be detected when only the CPU flag is set.");
    }
#endif

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
    public void TestTrimToNull(string? value, string? expected) =>
        Assert.Equal(expected, HostDetector.TrimToNull(value));

    [Theory]
    [MemberData(nameof(ParseFieldValueProcCpuInfoTestCases))]
    public void TestParseFieldValueProcCpuInfo(string? cpuInfo, string fieldName, string? expected) =>
        Assert.Equal(expected, HostDetector.ParseFieldValue(cpuInfo, fieldName));

    [Theory]
    [InlineData(WindowsCpuIdentifier, "Family", "25")]
    [InlineData(WindowsCpuIdentifier, "Model", "1")]
    [InlineData(WindowsCpuIdentifier, "Stepping", "1")]
    [InlineData(WindowsCpuIdentifier, "Vendor", null)]
    [InlineData("AMD64 Family", "Family", null)]
    [InlineData(null, "Family", null)]
    public void TestGetTokenAfter(string? text, string keyword, string? expected) =>
        Assert.Equal(expected, HostDetector.GetTokenAfter(text, keyword));

    [Theory]
    [MemberData(nameof(ParseFieldValueSysctlTestCases))]
    public void TestParseFieldValueSysctl(string? output, string key, string? expected) =>
        Assert.Equal(expected, HostDetector.ParseFieldValue(output, key));

    [Theory]
    [InlineData("1024 KB", 1048576)]
    [InlineData("6144 KB", 6291456)]
    [InlineData("6291456", 6291456)]
    [InlineData("12288000", 12288000)]
    [InlineData("1024K", 1048576)]
    [InlineData("unknown", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("9223372036854775807 K", null)]
    [InlineData("18014398509481985 K", null)]
    [InlineData("2097151 K", 2147482624)]
    [InlineData("2097152 K", null)]
    public void TestParseCacheSize(string? value, int? expected) =>
        Assert.Equal(expected, HostDetector.ParseCacheSize(value));

    [Fact]
    public void TestAddCpuInfoWindowsReadsEveryRegistryValue()
    {
        var attributes = new List<KeyValuePair<string, object>>();

        HostDetector.AddCpuInfoWindows(attributes, ReadWindowsCpuRegistryValue, () => 524288);

        var resourceAttributes = attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal("AuthenticAMD", resourceAttributes["host.cpu.vendor.id"]);
        Assert.Equal("AMD EPYC 7763 64-Core Processor", resourceAttributes["host.cpu.model.name"]);
        Assert.Equal("25", resourceAttributes["host.cpu.family"]);
        Assert.Equal("1", resourceAttributes["host.cpu.model.id"]);
        Assert.Equal("1", resourceAttributes["host.cpu.stepping"]);
        Assert.Equal(524288, Assert.IsType<int>(resourceAttributes["host.cpu.cache.l2.size"]));
        Assert.Equal(6, resourceAttributes.Count);
    }

    [Fact]
    public void TestAddCpuInfoWindowsEmitsCacheSizeWhenRegistryKeyIsMissing()
    {
        var attributes = new List<KeyValuePair<string, object>>();

        HostDetector.AddCpuInfoWindows(attributes, null, () => 524288);

        var attribute = Assert.Single(attributes);

        Assert.Equal("host.cpu.cache.l2.size", attribute.Key);
        Assert.Equal(524288, Assert.IsType<int>(attribute.Value));
    }

    [Fact]
    public void TestAddCpuInfoWindowsEmitsCacheSizeWhenRegistryReadThrows()
    {
        var attributes = new List<KeyValuePair<string, object>>();

        HostDetector.AddCpuInfoWindows(attributes, _ => throw new InvalidOperationException(), () => 524288);

        var attribute = Assert.Single(attributes);

        Assert.Equal("host.cpu.cache.l2.size", attribute.Key);
        Assert.Equal(524288, Assert.IsType<int>(attribute.Value));
    }

    private static string? ReadWindowsCpuRegistryValue(string name) => name switch
    {
        "VendorIdentifier" => "AuthenticAMD",
        "ProcessorNameString" => "AMD EPYC 7763 64-Core Processor                ",
        "Identifier" => WindowsCpuIdentifier,
        _ => null,
    };
}
