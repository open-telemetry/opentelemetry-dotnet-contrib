// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using OpenTelemetry.Resources;

namespace OpenTelemetry.ResourceDetectors.Host;

/// <summary>
/// Host detector.
/// </summary>
public sealed class HostDetector : IResourceDetector
{
    private const string ETC_MACHINEID = "/etc/machine-id";
    private const string ETC_VAR_DBUS_MACHINEID = "/var/lib/dbus/machine-id";
    private readonly PlatformID platformId;
    private readonly Func<IEnumerable<string>> getFilePaths;
    private readonly Func<string> getMacOsMachineId;
    private readonly Func<string> getWindowsMachineId;

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
    /// <param name="getMacOsMachineId">Function to get MacOS machine ID.</param>
    /// <param name="getWindowsMachineId">Function to get Windows machine ID.</param>
    internal HostDetector(PlatformID platformId, Func<IEnumerable<string>> getFilePaths, Func<string> getMacOsMachineId, Func<string> getWindowsMachineId)
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

            if (!string.IsNullOrEmpty(machineId))
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

    private static IEnumerable<string> GetFilePaths()
    {
        yield return ETC_MACHINEID;
        yield return ETC_VAR_DBUS_MACHINEID;
    }

    private static string? GetMachineIdMacOs()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sh",
                Arguments = "ioreg -rd1 -c IOPlatformExpertDevice | awk '/IOPlatformUUID/ { split($0, line, \"\\\"\"); printf(\"%s\\n\", line[4]); }'",
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
            return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false)?.GetValue("MachineGuid") as string ?? null;
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
            PlatformID.MacOSX => this.getMacOsMachineId(),
            PlatformID.Win32NT => this.getWindowsMachineId(),
            _ => null,
        };
    }

    private string GetMachineIdLinux()
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
