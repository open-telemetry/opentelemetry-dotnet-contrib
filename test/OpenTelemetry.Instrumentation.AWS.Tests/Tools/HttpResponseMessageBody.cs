// <copyright file="HttpResponseMessageBody.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
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
