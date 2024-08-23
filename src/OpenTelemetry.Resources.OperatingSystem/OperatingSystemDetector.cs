// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Runtime.InteropServices;
using System.Xml.Linq;
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
    ///
    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>(5);
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
                AddWindowsAttributes(attributes);
                break;
#if NET
            case OperatingSystemsValues.Linux:
                AddLinuxAttributes(attributes);
                break;
            case OperatingSystemsValues.Darwin:
                AddMacOSAttributes(attributes);
                break;
#endif
        }

        return new Resource(attributes);
    }

#if NET
    internal static string? GetOsReleaseValue(string[] osReleaseContent, string prefix)
    {
        foreach (var line in osReleaseContent)
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var fieldValue = line.Substring(prefix.Length);

                // Remove enclosing quotes.
                if (fieldValue.Length >= 2 &&
                    (fieldValue[0] == '"' || fieldValue[0] == '\'') &&
                    fieldValue[0] == fieldValue[^1])
                {
                    fieldValue = fieldValue[1..^1];
                }

                return fieldValue;
            }
        }

        return null;
    }

    internal static string? GetPlistValue(XElement dict, string key)
    {
        var keyElement = dict.Elements("key").FirstOrDefault(e => e.Value == key);
        if (keyElement != null)
        {
            var valueElement = keyElement.ElementsAfterSelf("string").FirstOrDefault();
            return valueElement?.Value;
        }

        return null;
    }
#endif

    private static void AddAttributeIfNotNullOrEmpty(List<KeyValuePair<string, object>> attributes, string key, object? value)
    {
        if (value == null)
        {
            OperatingSystemResourcesEventSource.Log.FailedToValidateValue("The provided value is null");
            return;
        }

        if (value is string strValue && string.IsNullOrEmpty(strValue))
        {
            OperatingSystemResourcesEventSource.Log.FailedToValidateValue("The provided value string is empty.");
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
    private static void AddWindowsAttributes(List<KeyValuePair<string, object>> attributes)
    {
        try
        {
            var registryKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryKey);
            if (key != null)
            {
                AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, key.GetValue("CurrentBuildNumber")?.ToString());
                AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, key.GetValue("ProductName")?.ToString());
                AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, key.GetValue("CurrentVersion")?.ToString());
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Windows attributes", ex);
        }
    }
#pragma warning restore CA1416

#if NET
    private static void AddLinuxAttributes(List<KeyValuePair<string, object>> attributes)
    {
        try
        {
            var osReleaseContent = File.ReadAllLines("/etc/os-release");
            if (osReleaseContent == null || osReleaseContent.Length == 0)
            {
                OperatingSystemResourcesEventSource.Log.FailedToFindFile("Failed to find or read the os-release file");
                return;
            }

            string? name = GetOsReleaseValue(osReleaseContent, "NAME=") ?? "Linux";
            string? version = GetOsReleaseValue(osReleaseContent, "VERSION_ID=");
            string? buildId = GetOsReleaseValue(osReleaseContent, "BUILD_ID=");

            // TODO: fallback for buildId

            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, buildId);
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, name);
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, version);
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Linux attributes", ex);
        }
    }

    private static void AddMacOSAttributes(List<KeyValuePair<string, object>> attributes)
    {
        try
        {
            var possibleFiles = new[]
            {
            "/System/Library/CoreServices/SystemVersion.plist",
            "/System/Library/CoreServices/ServerVersion.plist",
            };

            string? plistFilePath = possibleFiles.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(plistFilePath))
            {
                OperatingSystemResourcesEventSource.Log.FailedToFindFile("No suitable plist file found");
                return;
            }

            XDocument doc = XDocument.Load(plistFilePath);
            var dict = doc.Root?.Element("dict");

            if (dict == null)
            {
                OperatingSystemResourcesEventSource.Log.FailedToFindFile("No <dict> element found in plist file");
                return;
            }

            string? name = GetPlistValue(dict, "ProductName");
            string? version = GetPlistValue(dict, "ProductVersion");
            string? buildId = GetPlistValue(dict, "ProductBuildVersion");

            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, buildId);
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, name);
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, version);
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get MacOS attributes", ex);
        }
    }
#endif

    private static string GetOSDescription()
    {
#if NET
        return RuntimeInformation.OSDescription;
#else
        return Environment.OSVersion.ToString();
#endif
    }
}
