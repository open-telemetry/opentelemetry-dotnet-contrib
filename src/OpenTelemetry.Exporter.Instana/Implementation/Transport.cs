// <copyright file="Transport.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal class Transport
{
    private const int MULTI_SPAN_BUFFER_SIZE = 4096000;
    private const int MULTI_SPAN_BUFFER_LIMIT = 4070000;
    private readonly byte[] tracesBuffer = new byte[MULTI_SPAN_BUFFER_SIZE];
    private static readonly InstanaSpanSerializer InstanaSpanSerializer = new InstanaSpanSerializer();
    private static readonly MediaTypeHeaderValue MEDIAHEADER = new MediaTypeHeaderValue("application/json");

    private static bool isConfigured = false;
    private static int backendTimeout = 0;
    private static string configuredEndpoint = string.Empty;
    private static string configuredAgentKey = string.Empty;
    private static string bundleUrl = string.Empty;
    private static InstanaHttpClient client = null;

    static Transport()
    {
        Configure();
    }

    internal bool IsAvailable
    {
        get { return isConfigured && client != null; }
    }

    internal async Task SendSpansAsync(ConcurrentQueue<InstanaSpan> spanQueue)
    {
        try
        {
            using (MemoryStream sendBuffer = new MemoryStream(tracesBuffer))
            {
                using (StreamWriter writer = new StreamWriter(sendBuffer))
                {
                    await writer.WriteAsync("{\"spans\":[");
                    bool first = true;

                    // peek instead of dequeue, because we don't yet know whether the next span
                    // fits within our MULTI_SPAN_BUFFER_LIMIT
                    while (spanQueue.TryPeek(out InstanaSpan span) && sendBuffer.Position < MULTI_SPAN_BUFFER_LIMIT)
                    {
                        if (!first)
                        {
                            await writer.WriteAsync(",");
                        }
                        await InstanaSpanSerializer.SerializeToStreamWriterAsync(span, writer);
                        await writer.FlushAsync();

                        first = false;
                        // Now we can dequeue. Note, this means we'll be giving up/losing
                        // this span if we fail to send for any reason.
                        spanQueue.TryDequeue(out _);
                    }
                    await writer.WriteAsync("]}");
                    await writer.FlushAsync();

                    long length = sendBuffer.Position;
                    sendBuffer.Position = 0;
                    sendBuffer.SetLength(length);

                    HttpContent content = new StreamContent(sendBuffer, (int)length);
                    content.Headers.ContentType = MEDIAHEADER;
                    content.Headers.Add("X-INSTANA-TIME", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

                    using (var httpMsg = new HttpRequestMessage()
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri(bundleUrl),
                    })
                    {
                        httpMsg.Content = content;
                        await client.SendAsync(httpMsg);
                    }
                }
            }
        }
        catch (Exception e)
        {
            InstanaExporterEventSource.Log.FailedExport(e);
        }
    }

    private static void Configure()
    {
        if (isConfigured)
        {
            return;
        }

        if (string.IsNullOrEmpty(configuredEndpoint))
        {
            configuredEndpoint = Environment.GetEnvironmentVariable(InstanaExporterConstants.ENVVAR_INSTANA_ENDPOINT_URL);
        }

        if (string.IsNullOrEmpty(configuredEndpoint))
        {
            return;
        }

        bundleUrl = configuredEndpoint + "/bundle";

        if (string.IsNullOrEmpty(configuredAgentKey))
        {
            configuredAgentKey = Environment.GetEnvironmentVariable(InstanaExporterConstants.ENVVAR_INSTANA_AGENT_KEY);
        }

        if (string.IsNullOrEmpty(configuredAgentKey))
        {
            return;
        }

        if (backendTimeout == 0)
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable(InstanaExporterConstants.ENVVAR_INSTANA_TIMEOUT), out backendTimeout))
            {
                backendTimeout = InstanaExporterConstants.BACKEND_DEFAULT_TIMEOUT;
            }
        }

        ConfigureBackendClient();
        isConfigured = true;
    }

    private static void ConfigureBackendClient()
    {
        if (client != null)
        {
            return;
        }

        var configuredHandler = new HttpClientHandler();
        string proxy = Environment.GetEnvironmentVariable(InstanaExporterConstants.ENVVAR_INSTANA_ENDPOINT_PROXY);
        if (Uri.TryCreate(proxy, UriKind.Absolute, out Uri proxyAddress))
        {
            configuredHandler.Proxy = new WebProxy(proxyAddress, true);
            configuredHandler.UseProxy = true;
#pragma warning disable SA1130 // Use lambda syntax
            configuredHandler.ServerCertificateCustomValidationCallback = delegate { return true; };
#pragma warning restore SA1130 // Use lambda syntax
        }

        client = new InstanaHttpClient(backendTimeout, configuredHandler);

        client.DefaultRequestHeaders.Add("X-INSTANA-KEY", configuredAgentKey);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal class InstanaHttpClient : HttpClient
#pragma warning restore SA1402 // File may only contain a single type
{
    public InstanaHttpClient(int timeout)
        : base()
    {
        this.Timeout = TimeSpan.FromMilliseconds(timeout);
        this.DefaultRequestHeaders.Add("X-INSTANA-NOTRACE", "1");
    }

    public InstanaHttpClient(int timeout, HttpClientHandler handler)
        : base(handler)
    {
        this.Timeout = TimeSpan.FromMilliseconds(timeout);
        this.DefaultRequestHeaders.Add("X-INSTANA-NOTRACE", "1");
    }
}
