// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

internal class Utils
{
    internal static object? GetTagValue(Activity activity, string tagName)
    {
        foreach (KeyValuePair<string, object?> tag in activity.TagObjects)
        {
            if (tag.Key.Equals(tagName, StringComparison.Ordinal))
            {
                return tag.Value;
            }
        }

        return null;
    }

    internal static string RemoveSuffix(string originalString, string suffix)
    {
        if (string.IsNullOrEmpty(originalString))
        {
            return string.Empty;
        }

        if (originalString.EndsWith(suffix, StringComparison.Ordinal))
        {
            return originalString.Substring(0, originalString.Length - suffix.Length);
        }

        return originalString;
    }

    /// <summary>
    /// Removes amazon prefix from service name. There are two type of service name.
    ///     Amazon.DynamoDbV2
    ///     AmazonS3
    ///     .
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <returns>String after removing Amazon prefix.</returns>
    internal static string RemoveAmazonPrefixFromServiceName(string serviceName)
    {
        return RemovePrefix(RemovePrefix(serviceName, "Amazon"), ".");
    }

    private static string RemovePrefix(string originalString, string prefix)
    {
        if (string.IsNullOrEmpty(originalString))
        {
            return string.Empty;
        }

        if (originalString.StartsWith(prefix, StringComparison.Ordinal))
        {
            return originalString.Substring(prefix.Length);
        }

        return originalString;
    }
}
