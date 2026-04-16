// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

using System.Buffers;
using Google.Protobuf;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Utils;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Internal.Transport.Http;

internal sealed class PlainHttpTransport : IOpAmpTransport, IDisposable
{
    private const string HeaderContentType = "Content-Type";
    private const string HeaderOpAmpInstanceUUID = "OpAMP-Instance-UID";

    private readonly Uri uri;
    private readonly HttpClient httpClient;
    private readonly FrameProcessor processor;
    private readonly OpAmpClientSettings settings;

    public PlainHttpTransport(OpAmpClientSettings settings, FrameProcessor processor)
    {
        Guard.ThrowIfNull(settings, nameof(settings));
        Guard.ThrowIfNull(processor, nameof(processor));

        this.uri = settings.ServerUrl;
        this.processor = processor;
        this.httpClient = settings.HttpClientFactory();
        this.settings = settings;
    }

    public async Task SendAsync<T>(T message, CancellationToken token)
        where T : IMessage<T>
    {
        var content = message.ToByteArray();

        using var byteContent = new ByteArrayContent(content);
        byteContent.Headers.Add(HeaderContentType, "application/x-protobuf");
        byteContent.Headers.Add(HeaderOpAmpInstanceUUID, this.settings.InstanceUid.ToString());

        using var request = new HttpRequestMessage(HttpMethod.Post, this.uri)
        {
            Content = byteContent,
        };

        // ResponseHeadersRead prevents HttpClient from buffering the entire response body
        // before we can enforce the transport size limit.
        using var response = await this.httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        // Check Content-Length before reading if the header is present.
        if (response.Content.Headers.ContentLength > TransportConstants.MaxMessageSize)
        {
            OpAmpClientEventSource.Log.OversizedResponseContentLengthReceived(response.Content.Headers.ContentLength.Value, TransportConstants.MaxMessageSize);
            throw new OpAmpOversizedResponseException(
                $"OpAMP server response Content-Length ({response.Content.Headers.ContentLength}) exceeds the maximum allowed size of {TransportConstants.MaxMessageSize} bytes.");
        }

        // Read the response body with a size cap to prevent uncontrolled memory allocation (CWE-789).
        // Content-Length can be absent or spoofed, so we enforce the limit during the read as well.
        var responseMessage = await ReadBoundedResponseAsync(response, token).ConfigureAwait(false);

        this.processor.OnServerFrame(responseMessage.AsSequence());
    }

    public void Dispose()
    {
        this.httpClient?.Dispose();
    }

    private static async Task<byte[]> ReadBoundedResponseAsync(HttpResponseMessage response, CancellationToken token)
    {
        var stream = await response.Content
#if NET
            .ReadAsStreamAsync(token)
#else
            .ReadAsStreamAsync()
#endif
            .ConfigureAwait(false);

#if NET
        await using (stream.ConfigureAwait(false))
#else
        using (stream)
#endif
        {
            var buffer = ArrayPool<byte>.Shared.Rent(TransportConstants.MaxMessageSize);
            try
            {
                var totalRead = 0;
                while (totalRead < TransportConstants.MaxMessageSize)
                {
                    var bytesRead = await stream
#if NET
                        .ReadAsync(buffer.AsMemory(totalRead, TransportConstants.MaxMessageSize - totalRead), token)
#else
                        .ReadAsync(buffer, totalRead, TransportConstants.MaxMessageSize - totalRead, token)
#endif
                        .ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        // End of stream - copy the exact number of bytes read.
                        OpAmpClientEventSource.Log.HttpResponseBytesReceived(totalRead);
                        var result = new byte[totalRead];
                        Buffer.BlockCopy(buffer, 0, result, 0, totalRead);
                        return result;
                    }

                    totalRead += bytesRead;
                }

                // We've read exactly MaxMessageSize bytes. Check if there's more data.
                var probe = new byte[1];
                var extra = await stream
#if NET
                    .ReadAsync(probe.AsMemory(0, 1), token)
#else
                    .ReadAsync(probe, 0, 1, token)
#endif
                    .ConfigureAwait(false);

                if (extra > 0)
                {
                    // + 1: we read exactly MaxMessageSize bytes and confirmed at least one more byte exists.
                    OpAmpClientEventSource.Log.OversizedResponseBodyReceived(TransportConstants.MaxMessageSize + 1, TransportConstants.MaxMessageSize);
                    throw new OpAmpOversizedResponseException(
                        $"OpAMP server response body exceeds the maximum allowed size of {TransportConstants.MaxMessageSize} bytes.");
                }

                OpAmpClientEventSource.Log.HttpResponseBytesReceived(totalRead);
                var exactResult = new byte[totalRead];
                Buffer.BlockCopy(buffer, 0, exactResult, 0, totalRead);
                return exactResult;
            }
            finally
            {
                // Clear the rented buffer to avoid leaking sensitive data, then return it to the pool.
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
    }
}
