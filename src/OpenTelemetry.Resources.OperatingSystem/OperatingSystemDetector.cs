// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Runtime.InteropServices;
#endif
#if NET
using System.Xml.Linq;
#endif
using static OpenTelemetry.Resources.OperatingSystem.OperatingSystemSemanticConventions;

namespace OpenTelemetry.Resources.OperatingSystem;

/// <summary>
/// Operating system detector.
/// </summary>
internal sealed class OperatingSystemDetector : IResourceDetector
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
#if NET
    private const string KernelOsRelease = "/proc/sys/kernel/osrelease";
    private static readonly string[] DefaultEtcOsReleasePaths =
    [
        "/etc/os-release",
        "/usr/lib/os-release"
    ];

    private static readonly string[] DefaultPlistFilePaths =
    [
        "/System/Library/CoreServices/SystemVersion.plist",
        "/System/Library/CoreServices/ServerVersion.plist"
    ];
#endif

    private readonly OperatingSystemCategory? osCategory;
    private readonly string? registryKey;
#if NET
    private readonly string? kernelOsRelease;
    private readonly string[]? etcOsReleasePaths;
    private readonly string[]? plistFilePaths;

#endif

    internal OperatingSystemDetector()
#if NET
        : this(GetOSCategory(), RegistryKey, KernelOsRelease, DefaultEtcOsReleasePaths, DefaultPlistFilePaths)
#else
        : this(GetOSCategory(), RegistryKey)
#endif
    {
    }

#if NET
    /// <summary>
    /// Initializes a new instance of the <see cref="OperatingSystemDetector"/> class for testing.
    /// </summary>
    /// <param name="osCategory">The target platform identifier, specifying the operating system type from SemanticConventions.</param>
    /// <param name="registryKey">The string path in the Windows Registry to retrieve specific Windows attributes.</param>
    /// <param name="kernelOsRelease">The string path to the file used to obtain Linux build id.</param>
    /// <param name="etcOsReleasePath">The string path to the file used to obtain Linux attributes.</param>
    /// <param name="plistFilePaths">An array of file paths used to retrieve MacOS attributes from plist files.</param>
    internal OperatingSystemDetector(OperatingSystemCategory? osCategory, string? registryKey, string? kernelOsRelease, string[]? etcOsReleasePath, string[]? plistFilePaths)
#else
    /// <summary>
    /// Initializes a new instance of the <see cref="OperatingSystemDetector"/> class for testing.
    /// </summary>
    /// <param name="osCategory">The target platform identifier, specifying the operating system type from SemanticConventions.</param>
    /// <param name="registryKey">The string path in the Windows Registry to retrieve specific Windows attributes.</param>
    internal OperatingSystemDetector(OperatingSystemCategory? osCategory, string? registryKey)
#endif
    {
        this.osCategory = osCategory;
        this.registryKey = registryKey;
#if NET
        this.kernelOsRelease = kernelOsRelease;
        this.etcOsReleasePaths = etcOsReleasePath;
        this.plistFilePaths = plistFilePaths;
#endif
    }

    /// <summary>
    /// Detects the resource attributes from the operating system.
    /// </summary>
    /// <returns>Resource with key-value pairs of resource attributes.</returns>
    ///
    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>(6);
        if (this.osCategory == null)
        {
            return Resource.Empty;
        }

        if (this.osCategory != OperatingSystemCategory.Linux
        && this.osCategory != OperatingSystemCategory.MacOS)
        {
#pragma warning disable CA1305 // Specify IFormatProvider
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemBuildId, Environment.OSVersion.Version.Build.ToString()));
#pragma warning restore CA1305 // Specify IFormatProvider
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemVersion, Environment.OSVersion.Version.ToString()));
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemDescription, GetOSDescription()));
        }

        if (this.osCategory == OperatingSystemCategory.Windows)
        {
            this.AddWindowsAttributes(attributes);
        }
#if NET
        else if (this.osCategory == OperatingSystemCategory.AppleOS)
        {
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, GetOSName());
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemFamily, OSFamilyApple));
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemType, "unix"));
        }
        else if (this.osCategory == OperatingSystemCategory.Android)
        {
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemName, "Android"));
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemFamily, OSFamilyAndroid));
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemType, "unix"));
        }
        else if (this.osCategory == OperatingSystemCategory.Linux)
        {
            this.AddLinuxAttributes(attributes);
        }
        else if (this.osCategory == OperatingSystemCategory.MacOS)
        {
            this.AddMacOSAttributes(attributes);
        }
#endif

        return new Resource(attributes);
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

    private static OperatingSystemCategory? GetOSCategory()
    {
#if NET
        if (System.OperatingSystem.IsWindows())
        {
            return OperatingSystemCategory.Windows;
        }
        else if (System.OperatingSystem.IsIOS()
            || System.OperatingSystem.IsTvOS()
            || System.OperatingSystem.IsWatchOS())
        {
            return OperatingSystemCategory.AppleOS;
        }
        else if (System.OperatingSystem.IsAndroid())
        {
            return OperatingSystemCategory.Android;
        }
        else if (System.OperatingSystem.IsLinux() || System.OperatingSystem.IsFreeBSD())
        {
            return OperatingSystemCategory.Linux;
        }
        else if (System.OperatingSystem.IsMacOS())
        {
            return OperatingSystemCategory.MacOS;
        }

        return null;
#else
        return OperatingSystemCategory.Windows;
#endif
    }

#if NET
    private static string GetOSName()
    {
        if (System.OperatingSystem.IsIOS())
        {
            return "iOS";
        }
        else if (System.OperatingSystem.IsTvOS())
        {
            return "tvOS";
        }
        else if (System.OperatingSystem.IsWatchOS())
        {
            return "watchOS";
        }

        return "UNKNOWN";
    }
#endif

    private static string GetOSDescription() =>
#if NET
        RuntimeInformation.OSDescription;
#else
        Environment.OSVersion.ToString();
#endif

#pragma warning disable CA1308
#pragma warning disable CA1416
    private void AddWindowsAttributes(List<KeyValuePair<string, object>> attributes)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(this.registryKey!);
            if (key != null)
            {
                AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, key.GetValue("ProductName")?.ToString());
            }
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get Windows attributes", ex);
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemName, "windows"));
        }

        PlatformID osType = Environment.OSVersion.Platform;

        var osTypeString = osType.ToString();
        if (osTypeString.StartsWith("win", StringComparison.OrdinalIgnoreCase))
        {
            osTypeString = string.Concat("windows", osTypeString.Substring(3)); // Convert "win32nt" to "windows32nt" etc
        }

        attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemType, osTypeString.ToLowerInvariant()));
        attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemFamily, OSFamilyWindows));
    }
#pragma warning restore CA1416
#pragma warning restore CA1308

#if NET
    // based on:
    // https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Linux/os-release/Interop.OSReleaseFile.cs
    private void AddLinuxAttributes(List<KeyValuePair<string, object>> attributes)
    {
        try
        {
            var etcOsReleasePath = this.etcOsReleasePaths!.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(etcOsReleasePath))
            {
                OperatingSystemResourcesEventSource.Log.FailedToFindFile("Failed to find the os-release file");
                return;
            }

            var osReleaseContent = File.ReadAllLines(etcOsReleasePath);
            ReadOnlySpan<char> buildId = default, name = default, version = default, description = default, like = default;

            foreach (var line in osReleaseContent)
            {
                var lineSpan = line.AsSpan();

                _ = TryGetFieldValue(lineSpan, "BUILD_ID=", ref buildId) ||
                    TryGetFieldValue(lineSpan, "NAME=", ref name) ||
                    TryGetFieldValue(lineSpan, "VERSION_ID=", ref version) ||
                    TryGetFieldValue(lineSpan, "ID_LIKE=", ref like) ||
                    TryGetFieldValue(lineSpan, "PRETTY_NAME=", ref description);
            }

            var buildIdContent = buildId.IsEmpty ? File.ReadAllText(this.kernelOsRelease!).Trim() : buildId.ToString();

            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, buildIdContent);
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemDescription, description.IsEmpty ? null : description.ToString());
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, name.IsEmpty ? "Linux" : name.ToString());
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemFamily, like.IsEmpty ? null : like.ToString().Split(" "));
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, version.IsEmpty ? null : version.ToString());

            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemType, "unix"));
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

            var fieldValue = line.Slice(prefix.Length);

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

    private void AddMacOSAttributes(List<KeyValuePair<string, object>> attributes)
    {
        try
        {
            var plistFilePath = this.plistFilePaths!.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(plistFilePath))
            {
                OperatingSystemResourcesEventSource.Log.FailedToFindFile("No suitable plist file found");
                return;
            }

            var doc = XDocument.Load(plistFilePath);
            var dict = doc.Root?.Element("dict");

            string? buildId = null, name = null, version = null;

            if (dict != null)
            {
                var keys = dict.Elements("key").ToList();
                var values = dict.Elements("string").ToList();

                if (keys.Count != values.Count)
                {
                    OperatingSystemResourcesEventSource.Log.FailedToValidateValue($"Failed to get MacOS attributes: The number of keys does not match the number of values. Keys count: {keys.Count}, Values count: {values.Count}");
                    return;
                }

                for (var i = 0; i < keys.Count; i++)
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
                        default:
                            break;
                    }
                }
            }

            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemBuildId, buildId);
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemName, name);
            AddAttributeIfNotNullOrEmpty(attributes, AttributeOperatingSystemVersion, version);

            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemFamily, OSFamilyApple));
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemType, "unix"));
            attributes.Add(new KeyValuePair<string, object>(AttributeOperatingSystemDescription, GetOSDescription()));
        }
        catch (Exception ex)
        {
            OperatingSystemResourcesEventSource.Log.ResourceAttributesExtractException("Failed to get MacOS attributes", ex);
        }
    }
#endif
}
