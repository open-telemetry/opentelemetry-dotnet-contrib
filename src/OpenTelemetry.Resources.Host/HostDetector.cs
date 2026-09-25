// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Diagnostics;
using System.Text;
#endif
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Versioning;
#endif
using Microsoft.Win32;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources.Host;

/// <summary>
/// Host detector.
/// </summary>
internal sealed class HostDetector : IResourceDetector
{
    internal const string EnableNetworkAddressesEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_NETWORK_ADDRESSES";
    internal const string EnableCpuInfoEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_CPU_INFO";

    private const string WindowsCpuRegistryKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

#if !NETFRAMEWORK
    private const string ETCMACHINEID = "/etc/machine-id";
    private const string ETCVARDBUSMACHINEID = "/var/lib/dbus/machine-id";
    private const string MacOsCacheSizeKey = "hw.l2cachesize";
#endif

    private const int MaxBaseAttributeCount = 3;
    private const int MaxNetworkAddressAttributeCount = 2;
    private const int MaxCpuInfoAttributeCount = 6;

    private static readonly Version SemanticConventionsVersion = new(1, 44, 0);

#if !NETFRAMEWORK
    // The sysctl arguments are built from this map, so an attribute can never read a key the
    // command did not ask for.
    private static readonly KeyValuePair<string, string>[] MacOsCpuAttributes =
    [
        new("machdep.cpu.vendor", HostSemanticConventions.AttributeHostCpuVendorId),
        new("machdep.cpu.family", HostSemanticConventions.AttributeHostCpuFamily),
        new("machdep.cpu.model", HostSemanticConventions.AttributeHostCpuModelId),
        new("machdep.cpu.brand_string", HostSemanticConventions.AttributeHostCpuModelName),
        new("machdep.cpu.stepping", HostSemanticConventions.AttributeHostCpuStepping),
    ];

    private readonly Func<OSPlatform, bool> isOsPlatform;
    private readonly Func<IEnumerable<string>> getFilePaths;
    private readonly Func<string?> getMacOsMachineId;
#endif
    private readonly Func<string?> getWindowsMachineId;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostDetector"/> class.
    /// </summary>
    public HostDetector()
        : this(
#if !NETFRAMEWORK
        RuntimeInformation.IsOSPlatform,
        GetFilePaths,
        GetMachineIdMacOs,
#endif
        GetMachineIdWindows)
    {
    }

#if !NETFRAMEWORK
    public HostDetector(
        Func<IEnumerable<string>> getFilePaths,
        Func<string?> getMacOsMachineId,
        Func<string?> getWindowsMachineId)
        : this(
            RuntimeInformation.IsOSPlatform,
            getFilePaths,
            getMacOsMachineId,
            getWindowsMachineId)
    {
    }
#endif

    internal HostDetector(
#if !NETFRAMEWORK
        Func<OSPlatform, bool> isOsPlatform,
        Func<IEnumerable<string>> getFilePaths,
        Func<string?> getMacOsMachineId,
#endif
        Func<string?> getWindowsMachineId)
    {
#if !NETFRAMEWORK
        Guard.ThrowIfNull(isOsPlatform);
        Guard.ThrowIfNull(getFilePaths);
        Guard.ThrowIfNull(getMacOsMachineId);
#endif
        Guard.ThrowIfNull(getWindowsMachineId);

#if !NETFRAMEWORK
        this.isOsPlatform = isOsPlatform;
        this.getFilePaths = getFilePaths;
        this.getMacOsMachineId = getMacOsMachineId;
#endif
        this.getWindowsMachineId = getWindowsMachineId;
    }

#if !NETFRAMEWORK
    public static string? MapArchitectureToOtel(Architecture arch) =>
        arch switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "amd64",
            Architecture.Arm => "arm32",
            Architecture.Arm64 => "arm64",
#if NET
            Architecture.S390x => "s390x",
            Architecture.Armv6 => "arm32",
            Architecture.Ppc64le => "ppc64",

            // The following architectures do not have a mapping in OTel spec: https://github.com/open-telemetry/semantic-conventions/blob/v1.39.0/docs/resource/host.md
            Architecture.Wasm => null,
            Architecture.LoongArch64 => null,
#if NET10_0_OR_GREATER
            Architecture.RiscV64 => null,
#endif
#endif
            _ => null,
        };
#endif

    /// <summary>
    /// Detects the resource attributes from host.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        try
        {
            var networkAddressesEnabled = IsNetworkAddressesEnabled();
            var cpuInfoEnabled = IsCpuInfoEnabled();
            var capacity = MaxBaseAttributeCount
                + (networkAddressesEnabled ? MaxNetworkAddressAttributeCount : 0)
                + (cpuInfoEnabled ? MaxCpuInfoAttributeCount : 0);

            var attributes = new List<KeyValuePair<string, object>>(capacity)
            {
                new(HostSemanticConventions.AttributeHostName, Environment.MachineName),
            };

            var machineId = this.GetMachineId();

            if (machineId != null && !string.IsNullOrEmpty(machineId))
            {
                attributes.Add(new(HostSemanticConventions.AttributeHostId, machineId));
            }

#if !NETFRAMEWORK
            var arch = MapArchitectureToOtel(RuntimeInformation.OSArchitecture);
            if (arch != null)
            {
                attributes.Add(new(HostSemanticConventions.AttributeHostArch, arch));
            }
#endif
#if NET471_OR_GREATER
#error Architecture is available in .NET Framework 4.7.1+, enable it when we move to that as minimum supported version
#endif

            if (networkAddressesEnabled)
            {
                AddNetworkAddresses(attributes);
            }

            if (cpuInfoEnabled)
            {
                AddCpuInfo(attributes);
            }

            return new Resource(attributes, SchemaUrls.Get(SemanticConventionsVersion));
        }
        catch (InvalidOperationException ex)
        {
            // Handling InvalidOperationException due to https://learn.microsoft.com/dotnet/api/system.environment.machinename#exceptions
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return Resource.Empty;
    }

    internal static bool ShouldIncludeNetworkInterface(OperationalStatus operationalStatus, NetworkInterfaceType networkInterfaceType) =>
        operationalStatus == OperationalStatus.Up && networkInterfaceType != NetworkInterfaceType.Loopback;

    internal static bool ShouldIncludeIpAddress(IPAddress address) =>
        !IPAddress.IsLoopback(address);

    internal static string? FormatPhysicalAddress(PhysicalAddress physicalAddress)
    {
        var addressBytes = physicalAddress.GetAddressBytes();

        // BitConverter renders the IEEE RA format the specification requires, which
        // PhysicalAddress.ToString does not.
        return addressBytes.Length == 0 ? null : BitConverter.ToString(addressBytes);
    }

    // A copy built from the address bytes drops the IPv6 zone (%N) without changing the original.
    internal static string[] FormatIpAddresses(IEnumerable<IPAddress> addresses) =>
        RemoveDuplicates(addresses.Select(address => new IPAddress(address.GetAddressBytes()).ToString()));

    internal static string[] RemoveDuplicates(IEnumerable<string> values) =>
        values.Distinct().ToArray();

    internal static string? TrimToNull(string? value)
    {
        if (value == null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    internal static string? GetTokenAfter(string? text, string keyword)
    {
        if (text == null)
        {
            return null;
        }

        var tokens = text.Split([' '], StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < tokens.Length - 1; i++)
        {
            if (string.Equals(tokens[i], keyword, StringComparison.Ordinal))
            {
                return TrimToNull(tokens[i + 1]);
            }
        }

        return null;
    }

    internal static string? ParseFieldValue(string? text, string key)
    {
        if (text == null)
        {
            return null;
        }

        foreach (var line in text.Split('\n'))
        {
#if NET
            var separator = line.IndexOf(':', StringComparison.Ordinal);
#else
            var separator = line.IndexOf(':');
#endif
            if (separator < 0)
            {
                continue;
            }

            if (string.Equals(line.Substring(0, separator).Trim(), key, StringComparison.Ordinal))
            {
                return TrimToNull(line.Substring(separator + 1));
            }
        }

        return null;
    }

    internal static int? ParseCacheSize(string? value)
    {
        var normalized = TrimToNull(value);
        if (normalized == null)
        {
            return null;
        }

        var digits = 0;
        while (digits < normalized.Length && char.IsDigit(normalized[digits]))
        {
            digits++;
        }

#if NET
        if (digits == 0 || !long.TryParse(normalized.AsSpan(0, digits), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
#else
        if (digits == 0 || !long.TryParse(normalized.Substring(0, digits), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
#endif
        {
            return null;
        }

        // The sources disagree on units: Windows and macOS report bytes, /proc/cpuinfo reports
        // "1024 KB" and sysfs "1024K".
        var unit = normalized.Substring(digits).TrimStart();
        var multiplier = unit.Length == 0 ? 1 : char.ToUpperInvariant(unit[0]) switch
        {
            'K' => 1024,
            'M' => 1024 * 1024,
            _ => 0,
        };

        if (multiplier == 0)
        {
            return null;
        }

        // Checked before the multiply, which would otherwise wrap into an in-range value.
        if (size > int.MaxValue / multiplier)
        {
            return null;
        }

        return (int)(size * multiplier);
    }

    internal static void AddCpuInfoWindows(List<KeyValuePair<string, object>> attributes, Func<string, string?>? getRegistryValue, Func<int?> getL2CacheSize)
    {
        if (getRegistryValue != null)
        {
            try
            {
                AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuVendorId, TrimToNull(getRegistryValue("VendorIdentifier")));
                AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuModelName, TrimToNull(getRegistryValue("ProcessorNameString")));

                // Family, model and stepping have no registry value of their own; they are only
                // available inside "AMD64 Family 25 Model 1 Stepping 1", whose leading token is the
                // architecture rather than the vendor.
                var identifier = getRegistryValue("Identifier");
                AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuFamily, GetTokenAfter(identifier, "Family"));
                AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuModelId, GetTokenAfter(identifier, "Model"));
                AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuStepping, GetTokenAfter(identifier, "Stepping"));
            }
            catch (Exception ex)
            {
                HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
            }
        }

        AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuCacheL2Size, getL2CacheSize());
    }

#if !NETFRAMEWORK
    internal static string? ParseMacOsOutput(string? output)
    {
        if (output == null || string.IsNullOrEmpty(output))
        {
            return null;
        }

        var lines = output.Split([Environment.NewLine], StringSplitOptions.None);

        foreach (var line in lines)
        {
#if NET
            if (line.Contains("IOPlatformUUID", StringComparison.OrdinalIgnoreCase))
#else
            if (line.IndexOf("IOPlatformUUID", StringComparison.OrdinalIgnoreCase) >= 0)
#endif
            {
                var parts = line.Split('"');

                if (parts.Length > 3)
                {
                    return parts[3];
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetFilePaths()
    {
        yield return ETCMACHINEID;
        yield return ETCVARDBUSMACHINEID;
    }
#endif

    private static bool IsNetworkAddressesEnabled() =>
        bool.TryParse(Environment.GetEnvironmentVariable(EnableNetworkAddressesEnvVarName), out var enabled) && enabled;

    private static bool IsCpuInfoEnabled() =>
        bool.TryParse(Environment.GetEnvironmentVariable(EnableCpuInfoEnvVarName), out var enabled) && enabled;

    private static void AddNetworkAddresses(List<KeyValuePair<string, object>> attributes)
    {
        var ipAddresses = new List<IPAddress>();
        var macAddresses = new List<string>();

        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!ShouldIncludeNetworkInterface(networkInterface.OperationalStatus, networkInterface.NetworkInterfaceType))
                {
                    continue;
                }

                foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (ShouldIncludeIpAddress(unicastAddress.Address))
                    {
                        ipAddresses.Add(unicastAddress.Address);
                    }
                }

                var macAddress = FormatPhysicalAddress(networkInterface.GetPhysicalAddress());
                if (macAddress != null)
                {
                    macAddresses.Add(macAddress);
                }
            }
        }
        catch (Exception ex)
        {
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
            return;
        }

        if (ipAddresses.Count > 0)
        {
            attributes.Add(new(HostSemanticConventions.AttributeHostIp, FormatIpAddresses(ipAddresses)));
        }

        if (macAddresses.Count > 0)
        {
            attributes.Add(new(HostSemanticConventions.AttributeHostMac, RemoveDuplicates(macAddresses)));
        }
    }

    private static void AddCpuInfo(List<KeyValuePair<string, object>> attributes)
    {
#if NETFRAMEWORK
        AddCpuInfoWindows(attributes);
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddCpuInfoWindows(attributes);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            AddCpuInfoLinux(attributes);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            AddCpuInfoMacOs(attributes);
        }
#endif
    }

#if NET
    [SupportedOSPlatform("windows")]
#endif
    private static void AddCpuInfoWindows(List<KeyValuePair<string, object>> attributes)
    {
        RegistryKey? subKey = null;

        try
        {
            subKey = Registry.LocalMachine.OpenSubKey(WindowsCpuRegistryKey, false);
        }
        catch (Exception ex)
        {
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        using (subKey)
        {
            AddCpuInfoWindows(attributes, subKey == null ? null : name => subKey.GetValue(name) as string, GetL2CacheSizeWindows);
        }
    }

    private static int? GetL2CacheSizeWindows()
    {
        var buffer = IntPtr.Zero;

        try
        {
            uint length = 0;
            _ = NativeMethods.GetLogicalProcessorInformation(IntPtr.Zero, ref length);
            if (length == 0)
            {
                return null;
            }

            buffer = Marshal.AllocHGlobal((int)length);
            if (!NativeMethods.GetLogicalProcessorInformation(buffer, ref length))
            {
                return null;
            }

            var entrySize = Marshal.SizeOf<NativeMethods.SystemLogicalProcessorInformation>();
            for (var offset = 0; offset + entrySize <= length; offset += entrySize)
            {
                var entry = Marshal.PtrToStructure<NativeMethods.SystemLogicalProcessorInformation>(IntPtr.Add(buffer, offset));

                // The registry carries no cache value, and this API states the level rather
                // than leaving it to be inferred.
                if (entry.Relationship == NativeMethods.RelationCache && entry.Data.Cache.Level == 2)
                {
                    return entry.Data.Cache.Size <= int.MaxValue ? (int?)entry.Data.Cache.Size : null;
                }
            }
        }
        catch (Exception ex)
        {
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return null;
    }

#if !NETFRAMEWORK
#if NET
    [SupportedOSPlatform("linux")]
#endif
    private static void AddCpuInfoLinux(List<KeyValuePair<string, object>> attributes)
    {
        const string ProcCpuInfo = "/proc/cpuinfo";

        var cpuInfo = ReadCpuFile(ProcCpuInfo);

        // The processor blocks after the first repeat these fields for the other cores, so the
        // first match is the one to report.
        AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuVendorId, ParseFieldValue(cpuInfo, "vendor_id"));
        AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuFamily, ParseFieldValue(cpuInfo, "cpu family"));
        AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuModelId, ParseFieldValue(cpuInfo, "model"));
        AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuModelName, ParseFieldValue(cpuInfo, "model name"));
        AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuStepping, ParseFieldValue(cpuInfo, "stepping"));
        AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuCacheL2Size, GetL2CacheSizeLinux());
    }

    private static int? GetL2CacheSizeLinux()
    {
        const string SysfsCpuCache = "/sys/devices/system/cpu/cpu0/cache";

        try
        {
            if (!Directory.Exists(SysfsCpuCache))
            {
                return null;
            }

            foreach (var cacheDirectory in Directory.GetDirectories(SysfsCpuCache, "index*"))
            {
                // sysfs states the level, which /proc/cpuinfo's "cache size" leaves to be
                // guessed at: it is the last-level cache on some vendors and L2 on others.
                var level = TrimToNull(ReadCpuFile(Path.Combine(cacheDirectory, "level")));
                if (string.Equals(level, "2", StringComparison.Ordinal))
                {
                    return ParseCacheSize(ReadCpuFile(Path.Combine(cacheDirectory, "size")));
                }
            }
        }
        catch (Exception ex)
        {
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return null;
    }

    private static string? ReadCpuFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex)
        {
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return null;
    }

#if NET
    [SupportedOSPlatform("macos")]
#endif
    private static void AddCpuInfoMacOs(List<KeyValuePair<string, object>> attributes)
    {
        var output = GetCpuInfoMacOs();

        foreach (var attribute in MacOsCpuAttributes)
        {
            AddIfNotNull(attributes, attribute.Value, ParseFieldValue(output, attribute.Key));
        }

        AddIfNotNull(attributes, HostSemanticConventions.AttributeHostCpuCacheL2Size, ParseCacheSize(ParseFieldValue(output, MacOsCacheSizeKey)));
    }

    private static string? GetCpuInfoMacOs()
    {
        try
        {
            var timeoutMilliseconds = 5_000;
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/sbin/sysctl",
#if !NET
                Arguments = string.Join(" ", MacOsCpuAttributes.Select(attribute => attribute.Key)) + " " + MacOsCacheSizeKey,
#endif
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

#if NET
            foreach (var attribute in MacOsCpuAttributes)
            {
                startInfo.ArgumentList.Add(attribute.Key);
            }

            startInfo.ArgumentList.Add(MacOsCacheSizeKey);
#endif

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    HostResourceEventSource.Log.ProcessTimeout($"Process did not exit within {timeoutMilliseconds} ms");

                    try
                    {
                        // Disposing the wrapper leaves the child running.
                        process.Kill();
                    }
                    catch (Exception ex)
                    {
                        HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
                    }

                    return null;
                }

                // A key the machine does not have is reported on stderr while the keys it does
                // have are still written to stdout, so stderr is not read as a failure here.
                return process.StandardOutput.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return null;
    }
#endif

    private static void AddIfNotNull(List<KeyValuePair<string, object>> attributes, string key, string? value)
    {
        if (value != null)
        {
            attributes.Add(new(key, value));
        }
    }

    private static void AddIfNotNull(List<KeyValuePair<string, object>> attributes, string key, int? value)
    {
        if (value != null)
        {
            attributes.Add(new(key, value.Value));
        }
    }

#if !NETFRAMEWORK
    private static string? GetMachineIdMacOs()
    {
        try
        {
            var timeoutMilliseconds = 5_000;
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/sbin/ioreg",
                Arguments = "-rd1 -c IOPlatformExpertDevice",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var sb = new StringBuilder();
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var isExited = process.WaitForExit(timeoutMilliseconds);
                if (isExited)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(error))
                    {
                        HostResourceEventSource.Log.FailedToExtractResourceAttributes(nameof(HostDetector), error);
                        return null;
                    }

                    sb.Append(output);
                    return sb.ToString();
                }
                else
                {
                    HostResourceEventSource.Log.ProcessTimeout("Process did not exit within the given timeout");
                    return null;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return null;
    }
#endif

    private static string? GetMachineIdWindows()
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
#endif

        try
        {
            using var subKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false);
            return subKey?.GetValue("MachineGuid") as string ?? null;
        }
        catch (Exception ex)
        {
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return null;
    }

    private string? GetMachineId() =>
#if NETFRAMEWORK
        this.getWindowsMachineId();
#else
        this.isOsPlatform(OSPlatform.Windows) ? this.getWindowsMachineId() :
        this.isOsPlatform(OSPlatform.Linux) ? this.GetMachineIdLinux() :
        this.isOsPlatform(OSPlatform.OSX) ? ParseMacOsOutput(this.getMacOsMachineId()) : null;
#endif

#if !NETFRAMEWORK
    private string? GetMachineIdLinux()
    {
        var paths = this.getFilePaths();

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    return File.ReadAllText(path).Trim();
                }
                catch (Exception ex)
                {
                    HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
                }
            }
        }

        return null;
    }
#endif

    private static class NativeMethods
    {
        internal const int RelationCache = 2;

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint returnLength);

        [StructLayout(LayoutKind.Sequential)]
        internal struct CacheDescriptor
        {
            internal byte Level;
            internal byte Associativity;
            internal ushort LineSize;
            internal uint Size;
            internal uint Type;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        internal struct ProcessorInformationUnion
        {
            [FieldOffset(0)]
            internal CacheDescriptor Cache;

            // The native union's ULONGLONG Reserved[2] is what gives it 8-byte alignment, so
            // without these the union is laid out at offset 12 of the parent instead of 16.
            [FieldOffset(0)]
            internal ulong Reserved0;

            [FieldOffset(8)]
            internal ulong Reserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SystemLogicalProcessorInformation
        {
            internal UIntPtr ProcessorMask;
            internal int Relationship;
            internal ProcessorInformationUnion Data;
        }
    }
}
