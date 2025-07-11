// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
#if !NETFRAMEWORK
using System.Runtime.InteropServices;
#endif
using System.Text;
using Microsoft.Win32;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources.Host;

/// <summary>
/// Host detector.
/// </summary>
internal sealed class HostDetector : IResourceDetector
{
    private const string ETCMACHINEID = "/etc/machine-id";
    private const string ETCVARDBUSMACHINEID = "/var/lib/dbus/machine-id";
#if !NETFRAMEWORK
    private readonly Func<OSPlatform, bool> isOsPlatform;
#endif
    private readonly Func<IEnumerable<string>> getFilePaths;
    private readonly Func<string?> getMacOsMachineId;
    private readonly Func<string?> getWindowsMachineId;
    private readonly HostDetectorOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostDetector"/> class.
    /// </summary>
    /// <param name="options">The <see cref="HostDetectorOptions"/> which control's the behaviour of the resource detector.</param>
    public HostDetector(HostDetectorOptions options)
        : this(
#if !NETFRAMEWORK
            RuntimeInformation.IsOSPlatform,
#endif
            GetFilePaths,
            GetMachineIdMacOs,
            GetMachineIdWindows,
            options)
    {
    }

#if !NETFRAMEWORK
    public HostDetector(
        Func<IEnumerable<string>> getFilePaths,
        Func<string?> getMacOsMachineId,
        Func<string?> getWindowsMachineId,
        HostDetectorOptions options)
        : this(
            RuntimeInformation.IsOSPlatform,
            getFilePaths,
            getMacOsMachineId,
            getWindowsMachineId,
            options)
    {
    }
#endif

    internal HostDetector(
#if !NETFRAMEWORK
        Func<OSPlatform, bool> isOsPlatform,
#endif
        Func<IEnumerable<string>> getFilePaths,
        Func<string?> getMacOsMachineId,
        Func<string?> getWindowsMachineId,
        HostDetectorOptions options)
    {
#if !NETFRAMEWORK
        Guard.ThrowIfNull(isOsPlatform);
#endif
        Guard.ThrowIfNull(getFilePaths);
        Guard.ThrowIfNull(getMacOsMachineId);
        Guard.ThrowIfNull(getWindowsMachineId);

        this.options = options;

#if !NETFRAMEWORK
        this.isOsPlatform = isOsPlatform;
#endif
        this.getFilePaths = getFilePaths;
        this.getMacOsMachineId = getMacOsMachineId;
        this.getWindowsMachineId = getWindowsMachineId;
    }

    /// <summary>
    /// Detects the resource attributes from host.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>(4);
        var machineName = this.GetMachineName();
        if (!string.IsNullOrEmpty(machineName))
        {
            attributes.Add(new(HostSemanticConventions.AttributeHostName, machineName!));
        }

        var machineId = this.GetMachineId();

        if (!string.IsNullOrEmpty(machineId))
        {
            attributes.Add(new(HostSemanticConventions.AttributeHostId, machineId!));
        }

        if (this.options.IncludeIP)
        {
            var hostEntry = Dns.GetHostEntry(Environment.MachineName);
            var ips = hostEntry?.AddressList.Where(x => !IPAddress.IsLoopback(x))
                .Select(x => x.ToString())
                .Distinct()
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
            if (ips != null && ips.Length > 0)
            {
                attributes.Add(new(HostSemanticConventions.AttributeHostIp, ips));
            }
        }

        if (this.options.IncludeMac)
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            var macs = nics?.Select(x => string.Join(":", x.GetPhysicalAddress()
                    .GetAddressBytes()
                    .Select(y => y.ToString("X2", CultureInfo.InvariantCulture))))
                .Distinct()
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
            if (macs != null && macs.Length > 0)
            {
                attributes.Add(new(HostSemanticConventions.AttributeHostMac, macs));
            }
        }

        return new Resource(attributes);
    }

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

    private static string? GetMachineIdMacOs()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sh",
                Arguments = "-c \"ioreg -rd1 -c IOPlatformExpertDevice\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var sb = new StringBuilder();
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var isExited = process.WaitForExit(5000);
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

#pragma warning disable CA1416
    // stylecop wants this protected by System.OperatingSystem.IsWindows
    // this type only exists in .NET 5+
    private static string? GetMachineIdWindows()
    {
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
#pragma warning restore CA1416

    private string? GetMachineId()
    {
#if NETFRAMEWORK
        return this.getWindowsMachineId();
#else
        return this.isOsPlatform(OSPlatform.Windows) ? this.getWindowsMachineId() :
            this.isOsPlatform(OSPlatform.Linux) ? this.GetMachineIdLinux() :
            this.isOsPlatform(OSPlatform.OSX) ? ParseMacOsOutput(this.getMacOsMachineId()) : null;

#endif
    }

    private string? GetMachineName()
    {
        if (!string.IsNullOrEmpty(this.options.Name))
        {
            return this.options.Name;
        }

        try
        {
            return Environment.MachineName;
        }
        catch (InvalidOperationException ex)
        {
            // Handling InvalidOperationException due to https://learn.microsoft.com/en-us/dotnet/api/system.environment.machinename#exceptions
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return null;
    }

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
}
