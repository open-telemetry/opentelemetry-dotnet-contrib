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
#if NET
    private const string EtcOsReleasePath = "/etc/os-release";
#endif

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

    internal static string? GetOSType()
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
    internal static void AddWindowsAttributes(List<KeyValuePair<string, object>> attributes)
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
    internal static void AddLinuxAttributes(List<KeyValuePair<string, object>> attributes, string etcOsReleasePath = EtcOsReleasePath)
    {
        try
        {
            if (!File.Exists(etcOsReleasePath))
            {
                OperatingSystemResourcesEventSource.Log.FailedToFindFile("Failed to find or read the os-release file");
                return;
            }

            var osReleaseContent = File.ReadAllLines(etcOsReleasePath);
            ReadOnlySpan<char> buildId = default, name = default, version = default;

            foreach (var line in osReleaseContent)
            {
                ReadOnlySpan<char> lineSpan = line.AsSpan();

                _ = TryGetFieldValue(lineSpan, "BUILD_ID=", ref buildId) ||
                    TryGetFieldValue(lineSpan, "NAME=", ref name) ||
                    TryGetFieldValue(lineSpan, "VERSION_ID=", ref version);
            }

            // TODO: fallback for buildId

            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, buildId.IsEmpty ? null : buildId.ToString());
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, name.IsEmpty ? "Linux" : name.ToString());
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, version.IsEmpty ? null : version.ToString());
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Linux attributes", ex);
        }

        static bool TryGetFieldValue(ReadOnlySpan<char> line, ReadOnlySpan<char> prefix, ref ReadOnlySpan<char> value)
        {
            if (!line.StartsWith(prefix))
            {
                return false;
            }

            ReadOnlySpan<char> fieldValue = line.Slice(prefix.Length);

            // Remove enclosing quotes if present.
            if (fieldValue.Length >= 2 &&
                (fieldValue[0] == '"' || fieldValue[0] == '\'') &&
                fieldValue[0] == fieldValue[^1])
            {
                fieldValue = fieldValue[1..^1];
            }

            value = fieldValue;
            return true;
        }
    }

    internal static void AddMacOSAttributes(List<KeyValuePair<string, object>> attributes, string[]? plistFilePaths = null)
    {
        try
        {
            plistFilePaths ??= new[]
            {
                "/System/Library/CoreServices/SystemVersion.plist",
                "/System/Library/CoreServices/ServerVersion.plist",
            };

            string? plistFilePath = plistFilePaths.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(plistFilePath))
            {
                OperatingSystemResourcesEventSource.Log.FailedToFindFile("No suitable plist file found");
                return;
            }

            XDocument doc = XDocument.Load(plistFilePath);
            var dict = doc.Root?.Element("dict");

            string? buildId = null, name = null, version = null;

            if (dict != null)
            {
                var keys = dict.Elements("key").ToList();
                var values = dict.Elements("string").ToList();

                for (int i = 0; i < keys.Count; i++)
                {
                    switch (keys[i].Value)
                    {
                        case "ProductBuildVersion":
                            buildId = values[i].Value;
                            break;
                        case "ProductName":
                            name = values[i].Value;
                            break;
                        case "ProductVersion":
                            version = values[i].Value;
                            break;
                    }
                }
            }

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

    internal static string GetOSDescription()
    {
#if NET
        return RuntimeInformation.OSDescription;
#else
        return Environment.OSVersion.ToString();
#endif
    }

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
}
