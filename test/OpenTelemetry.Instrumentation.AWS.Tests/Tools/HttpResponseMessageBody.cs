// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime.Internal.Transform;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Tools;

internal class HttpResponseMessageBody : IHttpResponseBody
{
    private HttpClient? httpClient;
    private HttpResponseMessage response;
    private bool disposeClient;
    private bool disposed;

    public HttpResponseMessageBody(HttpResponseMessage response, HttpClient? httpClient, bool disposeClient)
    {
        this.httpClient = httpClient;
        this.response = response;
        this.disposeClient = disposeClient;
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    Stream IHttpResponseBody.OpenResponse()
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException("HttpWebResponseBody");
        }

        return this.response.Content.ReadAsStreamAsync().Result;
    }

    Task<Stream> IHttpResponseBody.OpenResponseAsync()
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException("HttpWebResponseBody");
        }

        if (this.response.Content != null)
        {
            return this.response.Content.ReadAsStreamAsync();
        }
        else
        {
            var ms = new MemoryStream();
            return Task.FromResult<Stream>(ms);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            if (this.response != null)
            {
                this.response.Dispose();
            }

            if (this.httpClient != null && this.disposeClient)
            {
                this.httpClient.Dispose();
            }

            this.disposed = true;
        }
    }
}
