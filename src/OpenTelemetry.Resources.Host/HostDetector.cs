// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace OpenTelemetry.Resources.Host;

/// <summary>
/// Host detector.
/// </summary>
internal sealed class HostDetector : IResourceDetector
{
    private const string ETCMACHINEID = "/etc/machine-id";
    private const string ETCVARDBUSMACHINEID = "/var/lib/dbus/machine-id";
    private readonly PlatformID platformId;
    private readonly Func<IEnumerable<string>> getFilePaths;
    private readonly Func<string?> getMacOsMachineId;
    private readonly Func<string?> getWindowsMachineId;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostDetector"/> class.
    /// </summary>
    public HostDetector()
        : this(
        Environment.OSVersion.Platform,
        GetFilePaths,
        GetMachineIdMacOs,
        GetMachineIdWindows)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HostDetector"/> class for testing.
    /// </summary>
    /// <param name="platformId">Target platform ID.</param>
    /// <param name="getFilePaths">Function to get Linux file paths to probe.</param>
    /// <param name="getMacOsMachineId">Function to get MacOS machine ID.</param>
    /// <param name="getWindowsMachineId">Function to get Windows machine ID.</param>
    internal HostDetector(PlatformID platformId, Func<IEnumerable<string>> getFilePaths, Func<string?> getMacOsMachineId, Func<string?> getWindowsMachineId)
    {
        this.platformId = platformId;
        this.getFilePaths = getFilePaths ?? throw new ArgumentNullException(nameof(getFilePaths));
        this.getMacOsMachineId = getMacOsMachineId ?? throw new ArgumentNullException(nameof(getMacOsMachineId));
        this.getWindowsMachineId = getWindowsMachineId ?? throw new ArgumentNullException(nameof(getWindowsMachineId));
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
#if NETFRAMEWORK
            if (line.IndexOf("IOPlatformUUID", StringComparison.OrdinalIgnoreCase) >= 0)
#else
            if (line.Contains("IOPlatformUUID", StringComparison.OrdinalIgnoreCase))
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
                Arguments = "ioreg -rd1 -c IOPlatformExpertDevice",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };

            var sb = new StringBuilder();
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            sb.Append(process?.StandardOutput.ReadToEnd());
            return sb.ToString();
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
        return this.platformId switch
        {
            PlatformID.Unix => this.GetMachineIdLinux(),
            PlatformID.MacOSX => ParseMacOsOutput(this.getMacOsMachineId()),
            PlatformID.Win32NT => this.getWindowsMachineId(),
            _ => null,
        };
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
