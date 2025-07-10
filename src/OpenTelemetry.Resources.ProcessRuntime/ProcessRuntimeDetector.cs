// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
#if NETFRAMEWORK
using Microsoft.Win32;
#endif

namespace OpenTelemetry.Resources.ProcessRuntime;

/// <summary>
/// Process runtime detector.
/// </summary>
internal sealed class ProcessRuntimeDetector : IResourceDetector
{
    /// <summary>
    /// Detects the resource attributes from .NET runtime.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    public Resource Detect()
    {
        var frameworkDescription = RuntimeInformation.FrameworkDescription;
        string netRuntimeVersion = string.Empty;
#if NETFRAMEWORK
        var netFrameworkVersion = GetNetFrameworkVersionFromRegistry();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        string netRuntimeName = ".NET Framework";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        if (netFrameworkVersion != null && !string.IsNullOrEmpty(netFrameworkVersion))
        {
            var lastSpace = netFrameworkVersion.LastIndexOf(' ');
            netRuntimeVersion = netFrameworkVersion.Substring(lastSpace + 1);
        }
        else
        {
            netRuntimeVersion = Environment.Version.ToString();
        }
#else
        netRuntimeVersion = Environment.Version.ToString();
#pragma warning disable CA1307 // Specify StringComparison for clarity
        string netRuntimeName = frameworkDescription.Replace(netRuntimeVersion, string.Empty).Trim();
#pragma warning restore CA1307 // Specify StringComparison for clarity
#endif

        return new Resource(
        [
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeDescription, frameworkDescription),
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeName, netRuntimeName),
            new(ProcessRuntimeSemanticConventions.AttributeProcessRuntimeVersion, netRuntimeVersion),
        ]);
    }

#if NETFRAMEWORK
    private static string? GetNetFrameworkVersionFromRegistry()
    {
        try
        {
            const string subKey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var ndpKey = baseKey.OpenSubKey(subKey);

            return ndpKey?.GetValue("Release") != null ? CheckFor45PlusVersion((int)ndpKey.GetValue("Release")) : null;
        }
        catch
        {
            return null;
        }
    }

    // Checking the version using >= enables forward compatibility.
    private static string? CheckFor45PlusVersion(int releaseKey)
    {
        return releaseKey switch
        {
            >= 533320 => "4.8.1",
            >= 528040 => "4.8",
            >= 461808 => "4.7.2",
            >= 461308 => "4.7.1",
            >= 460798 => "4.7",
            >= 394802 => "4.6.2",

            // Following versions are deprecated
            // >= 394254 => "4.6.1",
            // >= 393295 => "4.6",
            // >= 379893 => "4.5.2",
            // >= 378675 => "4.5.1",
            // >= 378389 => "4.5",

            // This code should never execute. A non-null release key should mean
            // that 4.5 or later is installed.
            _ => null,
        };
    }
#endif
}
