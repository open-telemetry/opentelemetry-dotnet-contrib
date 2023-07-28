// <copyright file="Utils.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
