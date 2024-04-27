// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
#if !NETFRAMEWORK
using System.Text.Json.Serialization.Metadata;
#endif
using System.Threading.Tasks;

namespace OpenTelemetry.ResourceDetectors;

/// <summary>
/// Class for resource detector utils.
/// </summary>
internal static class ResourceDetectorUtils
{
#if !NET6_0_OR_GREATER
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
#endif

    internal static async Task<string> SendOutRequest(string url, string method, KeyValuePair<string, string>? header, HttpClientHandler? handler = null)
    {
        using (var httpRequestMessage = new HttpRequestMessage())
        {
            httpRequestMessage.RequestUri = new Uri(url);
            httpRequestMessage.Method = new HttpMethod(method);
            if (header.HasValue)
            {
                httpRequestMessage.Headers.Add(header.Value.Key, header.Value.Value);
            }

#pragma warning disable CA2000 // Dispose objects before losing scope
            var httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
#pragma warning restore CA2000 // Dispose objects before losing scope
            using (var response = await httpClient.SendAsync(httpRequestMessage).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }

#if NET6_0_OR_GREATER
    internal static T? DeserializeFromFile<T>(string filePath, JsonTypeInfo<T> jsonTypeInfo)
    {
        using (var stream = GetStream(filePath))
        {
            return JsonSerializer.Deserialize(stream, jsonTypeInfo);
        }
    }

    internal static T? DeserializeFromString<T>(string json, JsonTypeInfo<T> jsonTypeInfo)
    {
        return JsonSerializer.Deserialize(json, jsonTypeInfo);
    }
#else
    internal static T? DeserializeFromFile<T>(string filePath)
    {
        using (var stream = GetStream(filePath))
        {
            return (T?)JsonSerializer.Deserialize(stream, typeof(T), JsonSerializerOptions);
        }
    }

    internal static T? DeserializeFromString<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
    }
#endif

    internal static Stream GetStream(string filePath)
    {
        return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }

    internal static StreamReader GetStreamReader(string filePath)
    {
        var fileStream = GetStream(filePath);
        var streamReader = new StreamReader(fileStream, Encoding.UTF8);
        return streamReader;
    }
}
