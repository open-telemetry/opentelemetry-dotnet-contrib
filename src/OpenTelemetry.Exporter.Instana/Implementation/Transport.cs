// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Globalization;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using System.Text.Json;

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal sealed class Transport : IDisposable
{
    private const int MultiSpanBufferSize = 4096000;
    private const int MultiSpanBufferLimit = 4070000;

    private static readonly MediaTypeHeaderValue MediaType = new("application/json");

    private readonly Uri bundleUri;
    private readonly InstanaExporterOptions options;

    private HttpClient? client;

    public Transport(InstanaExporterOptions options)
    {
        this.options = options;
        this.bundleUri = new Uri(options.EndpointUri, "bundle");
    }

    public void Dispose()
    {
        this.client?.Dispose();
        GC.SuppressFinalize(this);
    }

    public bool Send(List<InstanaSpan> batch)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(MultiSpanBufferSize);

        try
        {
            using var stream = new MemoryStream(buffer);
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            writer.WritePropertyName("spans");
            writer.WriteStartArray();

            int maxBatchSize = this.options.BatchExportProcessorOptions.MaxExportBatchSize;
            int written = 0;

            using var enumerator = batch.GetEnumerator();

            while ((writer.BytesCommitted + writer.BytesPending) < MultiSpanBufferLimit && written < maxBatchSize && enumerator.MoveNext())
            {
                InstanaSpanSerializer.Serialize(enumerator.Current, writer);
                written++;
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();

            var length = stream.Position;
            stream.Position = 0;
            stream.SetLength(length);

            using var content = new StreamContent(stream, (int)length);
            content.Headers.ContentType = MediaType;

            using var message = new HttpRequestMessage(HttpMethod.Post, this.bundleUri)
            {
                Content = content,
            };

            message.Headers.Add("X-INSTANA-KEY", this.options.AgentKey);
            message.Headers.Add("X-INSTANA-NOTRACE", "1");
            message.Headers.Add("X-INSTANA-TIME", this.options.UtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));

            this.client ??= this.CreateClient();

#if NET
            using var response = this.client.Send(message);
#else
#pragma warning disable CA2025 // Do not pass 'IDisposable' instances into unawaited tasks
            using var response = this.client.SendAsync(message).GetAwaiter().GetResult();
#pragma warning restore CA2025 // Do not pass 'IDisposable' instances into unawaited tasks
#endif

            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            InstanaExporterEventSource.Log.FailedExport(ex);
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private HttpClient CreateClient()
    {
        if (this.options.HttpClientFactory is { } factory)
        {
            return factory();
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        var handler = new HttpClientHandler()
        {
#if !NETFRAMEWORK
            CheckCertificateRevocationList = true,
#endif
        };
#pragma warning restore CA2000 // Dispose objects before losing scope

        if (this.options.ProxyUri is { } proxyAddress)
        {
            handler.Proxy = new WebProxy(proxyAddress, true);
            handler.UseProxy = true;
        }

#pragma warning disable CA5399 // .NET Framework does not support CheckCertificateRevocationList
        var client = new HttpClient(handler, disposeHandler: true);
#pragma warning restore CA5399 // .NET Framework does not support CheckCertificateRevocationList

        try
        {
            client.Timeout = TimeSpan.FromMilliseconds(this.options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds);
            return client;
        }
        catch (Exception)
        {
            client.Dispose();
            throw;
        }
    }
}
