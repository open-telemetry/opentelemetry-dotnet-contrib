// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
#endif
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources.Host;

/// <summary>
/// Host detector.
/// </summary>
internal sealed class HostDetector : IResourceDetector
{
    internal const string EnableNetworkAddressesEnvVarName = "OTEL_DOTNET_EXPERIMENTAL_HOST_RESOURCE_ENABLE_NETWORK_ADDRESSES";

#if !NETFRAMEWORK
    private const string ETCMACHINEID = "/etc/machine-id";
    private const string ETCVARDBUSMACHINEID = "/var/lib/dbus/machine-id";
#endif

    private static readonly Version SemanticConventionsVersion = new(1, 43, 0);

#if !NETFRAMEWORK
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

            var attributes = new List<KeyValuePair<string, object>>(networkAddressesEnabled ? 5 : 3)
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

    internal static bool ShouldIncludeIpAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal)
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            // 169.254.0.0/16 has no BCL predicate, unlike its IPv6 counterpart.
            var addressBytes = address.GetAddressBytes();
            return addressBytes[0] != 169 || addressBytes[1] != 254;
        }

        return true;
    }

    internal static string? FormatPhysicalAddress(PhysicalAddress physicalAddress)
    {
        var addressBytes = physicalAddress.GetAddressBytes();

        // BitConverter renders the IEEE RA format the specification requires, which
        // PhysicalAddress.ToString does not.
        return addressBytes.Length == 0 ? null : BitConverter.ToString(addressBytes);
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

    private static void AddNetworkAddresses(List<KeyValuePair<string, object>> attributes)
    {
        var ipAddresses = new List<string>();
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
                        ipAddresses.Add(unicastAddress.Address.ToString());
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
            attributes.Add(new(HostSemanticConventions.AttributeHostIp, ipAddresses.ToArray()));
        }

        if (macAddresses.Count > 0)
        {
            attributes.Add(new(HostSemanticConventions.AttributeHostMac, macAddresses.ToArray()));
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
}
