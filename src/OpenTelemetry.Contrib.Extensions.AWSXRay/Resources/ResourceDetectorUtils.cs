// <copyright file="ResourceDetectorUtils.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;

/// <summary>
/// Class for resource detector utils.
/// </summary>
#pragma warning disable CA1052
public class ResourceDetectorUtils
#pragma warning restore CA1052
{
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

    internal static T? DeserializeFromFile<T>(string filePath)
    {
        using (var streamReader = GetStreamReader(filePath))
        {
            JsonSerializer serializer = new JsonSerializer();
            return (T?)serializer.Deserialize(streamReader, typeof(T));
        }
    }

    internal static T? DeserializeFromString<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }

    internal static StreamReader GetStreamReader(string filePath)
    {
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var streamReader = new StreamReader(fileStream, Encoding.UTF8);
        return streamReader;
    }
}
