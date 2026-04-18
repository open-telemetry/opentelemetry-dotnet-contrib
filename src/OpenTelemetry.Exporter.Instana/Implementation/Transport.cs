// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;

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

    public async Task<int> SendAsync(ConcurrentQueue<InstanaSpan> batch, CancellationToken cancellationToken)
    {
        int written = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(MultiSpanBufferSize);

        try
        {
            using var sendBuffer = new MemoryStream(buffer);
            using var writer = new StreamWriter(sendBuffer);
            await writer.WriteAsync("{\"spans\":[").ConfigureAwait(false);

            int maxBatchSize = this.options.BatchExportProcessorOptions.MaxExportBatchSize;

            while (sendBuffer.Position < MultiSpanBufferLimit && written < maxBatchSize && batch.TryDequeue(out var span))
            {
                if (written > 0)
                {
                    await writer.WriteAsync(',').ConfigureAwait(false);
                }

                await InstanaSpanSerializer.SerializeToStreamWriterAsync(span, writer).ConfigureAwait(false);

#if NET
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
                await writer.FlushAsync().ConfigureAwait(false);
#endif

                written++;
            }

            await writer.WriteAsync("]}").ConfigureAwait(false);

#if NET
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
            await writer.FlushAsync().ConfigureAwait(false);
#endif

            var length = sendBuffer.Position;
            sendBuffer.Position = 0;
            sendBuffer.SetLength(length);

            using var content = new StreamContent(sendBuffer, (int)length);
            content.Headers.ContentType = MediaType;

            using var message = new HttpRequestMessage(HttpMethod.Post, this.bundleUri)
            {
                Content = content,
            };

            message.Headers.Add("X-INSTANA-KEY", this.options.AgentKey);
            message.Headers.Add("X-INSTANA-NOTRACE", "1");
            message.Headers.Add("X-INSTANA-TIME", this.options.UtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));

            this.client ??= this.CreateClient();

            using var response = await this.client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            InstanaExporterEventSource.Log.FailedExport(ex);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return written;
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
