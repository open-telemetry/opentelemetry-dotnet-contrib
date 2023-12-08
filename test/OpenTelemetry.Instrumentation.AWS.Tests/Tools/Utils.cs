// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Tools;

internal static class Utils
{
    public static Stream CreateStreamFromString(string s)
    {
        MemoryStream stream = new MemoryStream();
        StreamWriter writer = new StreamWriter(stream);
        writer.Write(s);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    public static Stream? GetResourceStream(string resourceName)
    {
        Assembly assembly = typeof(Utils).Assembly;
        var resource = FindResourceName(resourceName);
        Stream? stream = assembly.GetManifestResourceStream(resource);
        return stream;
    }

    public static string GetResourceText(string resourceName)
    {
        var stream = GetResourceStream(resourceName);
        if (stream == null)
        {
            return string.Empty;
        }

        using (StreamReader reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }

    public static string FindResourceName(string partialName)
    {
#pragma warning disable CA2249 // Consider using 'string.Contains' instead of 'string.IndexOf'
        return FindResourceName(s => s.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0).Single();
#pragma warning restore CA2249 // Consider using 'string.Contains' instead of 'string.IndexOf'
    }

    public static IEnumerable<string> FindResourceName(Predicate<string> match)
    {
        Assembly assembly = typeof(Utils).Assembly;
        var allResources = assembly.GetManifestResourceNames();
        foreach (var resource in allResources)
        {
            if (match(resource))
            {
                yield return resource;
            }
        }
    }

    public static object? GetTagValue(Activity activity, string tagName)
    {
        return AWS.Implementation.Utils.GetTagValue(activity, tagName);
    }
}
