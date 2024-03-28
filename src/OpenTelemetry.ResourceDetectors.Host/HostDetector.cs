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
    /// <summary>
    /// Detects the resource attributes from host.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        try
        {
            return new Resource(new List<KeyValuePair<string, object>>(1)
            {
                new(HostSemanticConventions.AttributeHostName, Environment.MachineName),
                new(HostSemanticConventions.AttributeHostId, GetMachineId()),
            });
        }
        catch (InvalidOperationException ex)
        {
            // Handling InvalidOperationException due to https://learn.microsoft.com/en-us/dotnet/api/system.environment.machinename#exceptions
            HostResourceEventSource.Log.ResourceAttributesExtractException(nameof(HostDetector), ex);
        }

        return Resource.Empty;
    }

    private static string GetMachineId()
    {
        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => GetMachineIdLinux(),
            PlatformID.MacOSX => GetMachineIdMacOs(),
            PlatformID.Win32NT => GetMachineIdWindows(),
            _ => string.Empty,
        };
    }

    private static string GetMachineIdLinux()
    {
        var paths = new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" };

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

        return string.Empty;
    }

    private static string GetMachineIdMacOs()
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

#pragma warning disable CA1416
    // stylecop wants this protected by System.OperatingSystem.IsWindows
    // this type only exists in .NET 5+
    private static string GetMachineIdWindows()
    {
        return Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false)?.GetValue("MachineGuid") as string ?? string.Empty;
    }
#pragma warning restore CA1416
}
