// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="HostDetector"/> class.
    /// </summary>
    public HostDetector()
        : this(
#if !NETFRAMEWORK
        RuntimeInformation.IsOSPlatform,
#endif
        GetFilePaths,
        GetMachineIdMacOs,
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
#endif
        Func<IEnumerable<string>> getFilePaths,
        Func<string?> getMacOsMachineId,
        Func<string?> getWindowsMachineId)
    {
#if !NETFRAMEWORK
        Guard.ThrowIfNull(isOsPlatform);
#endif
        Guard.ThrowIfNull(getFilePaths);
        Guard.ThrowIfNull(getMacOsMachineId);
        Guard.ThrowIfNull(getWindowsMachineId);

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
        try
        {
            var attributes = new List<KeyValuePair<string, object>>(2)
            {
                new(HostSemanticConventions.AttributeHostName, Environment.MachineName),
            };
            var machineId = this.GetMachineId();

            if (machineId != null && !string.IsNullOrEmpty(machineId))
            {
                attributes.Add(new(HostSemanticConventions.AttributeHostId, machineId));
            }

            return new Resource(attributes);
        }
        catch (InvalidOperationException ex)
        {
            // Handling InvalidOperationException due to https://learn.microsoft.com/en-us/dotnet/api/system.environment.machinename#exceptions
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return Resource.Empty;
    }

    internal static string? ParseMacOsOutput(string? output)
    {
        if (output == null || string.IsNullOrEmpty(output))
        {
            return null;
        }

        var lines = output.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

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
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

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
        if (this.isOsPlatform(OSPlatform.Windows))
        {
            return this.getWindowsMachineId();
        }

        if (this.isOsPlatform(OSPlatform.Linux))
        {
            return this.GetMachineIdLinux();
        }

        if (this.isOsPlatform(OSPlatform.OSX))
        {
            return ParseMacOsOutput(this.getMacOsMachineId());
        }

        return null;
#endif
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
