// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Diagnostics;
using System.Runtime.InteropServices;

#endif
using static OpenTelemetry.Resources.OperatingSystem.OperatingSystemSemanticConventions;

namespace OpenTelemetry.Resources.OperatingSystem;

/// <summary>
/// Operating system detector.
/// </summary>
internal sealed class OperatingSystemDetector : IResourceDetector
{
    /// <summary>
    /// Detects the resource attributes from the operating system.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>();
        var osType = GetOSType();
        if (osType == null)
        {
            return Resource.Empty;
        }

        attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemType, osType));

        AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemDescription, GetOSDescription());

        switch (osType)
        {
            case OperatingSystemsValues.Windows:
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, GetWindowsBuildId());
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, "Windows");
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, GetWindowsVersion());
            break;
#if !NETFRAMEWORK
            case OperatingSystemsValues.Linux:
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, GetLinuxBuildId());
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, GetLinuxDistributionName());
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, GetLinuxVersion());
            break;
            case OperatingSystemsValues.Darwin:
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, GetMacOSBuildId());
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, "MacOS");
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, GetMacOSVersion());
            break;
#endif
        }

        return new Resource(attributes);
    }

    private static void AddAttributeIfNotNullOrEmpty(List<KeyValuePair<string, object>> attributes, string key, object? value)
    {
        if (value is string strValue && string.IsNullOrEmpty(strValue))
        {
            OperatingSystemResourcesEventSource.Log.FailedToValidateValue("The provided value string is null or empty.");
            return;
        }

        attributes.Add(new KeyValuePair<string, object>(key, value!));
    }

    private static string? GetOSType()
    {
#if NETFRAMEWORK
        return OperatingSystemsValues.Windows;
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OperatingSystemsValues.Windows;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OperatingSystemsValues.Linux;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OperatingSystemsValues.Darwin;
        }
        else
        {
            return null;
        }
#endif
    }

#pragma warning disable CA1416
    private static string? GetWindowsBuildId()
    {
        var registryKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        var buildLabValue = "BuildLab";
        var buildLabExValue = "BuildLabEx";

        try
        {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKey))
            {
                if (key != null)
                {
                    var buildLab = key.GetValue(buildLabValue)?.ToString();
                    var buildLabEx = key.GetValue(buildLabExValue)?.ToString();
                    return !string.IsNullOrEmpty(buildLabEx) ? buildLabEx : buildLab;
                }
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Windows build ID", ex);
        }

        return null;
    }
#pragma warning restore CA1416

#if !NETFRAMEWORK
    private static string? GetLinuxBuildId()
    {
        try
        {
            var osReleaseContent = File.ReadAllText("/etc/os-release");
            foreach (var line in osReleaseContent.Split('\n'))
            {
                if (line.StartsWith("BUILD_ID=", StringComparison.Ordinal))
                {
                    return line.Substring("BUILD_ID=".Length).Trim('"');
                }
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Linux build ID", ex);
        }

        return null;
    }

    private static string? GetMacOSBuildId()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sw_vers",
                Arguments = "-buildVersion",
                RedirectStandardOutput = true,
            };
            using (var process = Process.Start(psi))
            {
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    return output;
                }
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get MacOS build ID", ex);
        }

        return null;
    }
#endif

    private static string GetOSDescription()
    {
#if !NETFRAMEWORK
        return RuntimeInformation.OSDescription;
#else
        return Environment.OSVersion.ToString();
#endif
    }

    private static string GetLinuxDistributionName()
    {
        try
        {
            var osReleaseFile = "/etc/os-release";
            if (File.Exists(osReleaseFile))
            {
                var lines = File.ReadAllLines(osReleaseFile);
                foreach (var line in lines)
                {
                    if (line.StartsWith("NAME=", StringComparison.Ordinal))
                    {
                        return line.Substring("NAME=".Length).Trim('"');
                    }
                }
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Linux distribution name", ex);
        }

        return "Linux";
    }

#pragma warning disable CA1416
    private static string? GetWindowsVersion()
    {
        try
        {
            var registryKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKey))
            {
                if (key != null)
                {
                    var currentVersion = key.GetValue("CurrentVersion")?.ToString();
                    var currentBuild = key.GetValue("CurrentBuild")?.ToString();
                    var ubr = key.GetValue("UBR")?.ToString();
                    return $"{currentVersion}.{currentBuild}.{ubr}";
                }
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Windows version", ex);
        }

        return null;
    }
#pragma warning restore CA1416

#if !NETFRAMEWORK
    private static string? GetMacOSVersion()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sw_vers",
                Arguments = "-productVersion",
                RedirectStandardOutput = true,
            };
            using (var process = Process.Start(psi))
            {
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    return output;
                }
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get MacOS version", ex);
        }

        return null;
    }

    private static string? GetLinuxVersion()
    {
        try
        {
            var osReleaseFile = "/etc/os-release";
            if (File.Exists(osReleaseFile))
            {
                var lines = File.ReadAllLines(osReleaseFile);
                foreach (var line in lines)
                {
                    if (line.StartsWith("VERSION_ID=", StringComparison.Ordinal))
                    {
                        return line.Substring("VERSION_ID=".Length).Trim('"');
                    }
                }
            }

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "uname",
                    Arguments = "-r",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output;
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Linux version", ex);
        }

        return null;
    }

#endif
}
