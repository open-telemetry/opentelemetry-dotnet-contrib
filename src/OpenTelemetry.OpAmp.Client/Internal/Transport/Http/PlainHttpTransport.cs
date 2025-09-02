// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

using Google.Protobuf;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Utils;

namespace OpenTelemetry.OpAmp.Client.Internal.Transport.Http;

internal sealed class PlainHttpTransport : IOpAmpTransport, IDisposable
{
    private readonly Uri uri;
    private readonly HttpClient httpClient;
    private readonly HttpClientHandler handler;
    private readonly FrameProcessor processor;

    public PlainHttpTransport(Uri serverUrl, FrameProcessor processor)
    {
        Guard.ThrowIfNull(serverUrl, nameof(serverUrl));
        Guard.ThrowIfNull(processor, nameof(processor));

        this.uri = serverUrl;
        this.processor = processor;
        this.handler = new HttpClientHandler
        {
            // Trust all certificates
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
        };

        this.httpClient = new HttpClient(this.handler);
    }

    public async Task SendAsync<T>(T message, CancellationToken token)
        where T : IMessage<T>
    {
        var content = message.ToByteArray();

        using var byteContent = new ByteArrayContent(content);
        byteContent.Headers.Add("Content-Type", "application/x-protobuf");

        var response = await this.httpClient
            .PostAsync(this.uri, byteContent, cancellationToken: token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var responseMessage = await response.Content
#if NET
            .ReadAsByteArrayAsync(token)
#else
            .ReadAsByteArrayAsync()
#endif
            .ConfigureAwait(false);

        this.processor.OnServerFrame(responseMessage.AsSequence());
    }

    public void Dispose()
    {
        this.httpClient?.Dispose();
        this.handler?.Dispose();
    }
}
