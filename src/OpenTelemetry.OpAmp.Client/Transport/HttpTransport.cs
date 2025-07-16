// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpenTelemetry.OpAmp.Client.Utils;

namespace OpenTelemetry.OpAmp.Client.Transport;

internal class HttpTransport : IOpAmpTransport, IDisposable
{
    private readonly Uri uri = new("https://localhost:4320/v1/opamp");
    private readonly HttpClient httpClient;
    private readonly HttpClientHandler handler;
    private readonly FrameProcessor processor;

    public HttpTransport(FrameProcessor processor)
    {
        this.processor = processor ?? throw new ArgumentNullException(nameof(processor));

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
        byteContent.Headers.Remove("Content-Type");
        byteContent.Headers.Add("Content-Type", "application/x-protobuf");

        var response = await this.httpClient
            .PostAsync(this.uri, byteContent, cancellationToken: token)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to send message: {response.ReasonPhrase}");
        }

        var responseMessage = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);

        this.processor.OnServerFrame(responseMessage.AsSequence(), responseMessage.Length, verifyHeader: false);
    }

    public void Dispose()
    {
        this.httpClient?.Dispose();
        this.handler?.Dispose();
    }
}
