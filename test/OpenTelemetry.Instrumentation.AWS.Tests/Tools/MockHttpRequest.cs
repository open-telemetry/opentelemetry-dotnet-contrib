// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
#if !NETFRAMEWORK
using System.Net.Http;
#endif
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Transform;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Tools;

#if NETFRAMEWORK
internal class MockHttpRequest : IHttpRequest<Stream>
{
    private Stream? requestStream;

    public MockHttpRequest(Uri requestUri, Action? action, Func<MockHttpRequest, HttpWebResponse?>? responseCreator = null)
    {
        this.RequestUri = requestUri;
        this.GetResponseAction = action;
        this.ResponseCreator = responseCreator ?? this.CreateResponse;
    }

    public bool IsDisposed { get; set; }

    public bool IsAborted { get; set; }

    public bool IsConfigureRequestCalled { get; set; }

    public bool IsSetRequestHeadersCalled { get; set; }

    public bool IsGetRequestContentCalled { get; set; }

    public string? Method { get; set; }

    public Uri RequestUri { get; set; }

    public Action? GetResponseAction { get; set; }

    public Func<MockHttpRequest, HttpWebResponse?> ResponseCreator { get; set; }

    public void ConfigureRequest(IRequestContext requestContext)
    {
        this.IsConfigureRequestCalled = true;
    }

    public void SetRequestHeaders(IDictionary<string, string> headers)
    {
        this.IsSetRequestHeadersCalled = true;
    }

    public Stream GetRequestContent()
    {
        this.IsGetRequestContentCalled = true;
        this.requestStream = new MemoryStream();
        return this.requestStream;
    }

    public IWebResponseData GetResponse()
    {
        if (this.GetResponseAction != null)
        {
            this.GetResponseAction();
        }

        var response = this.ResponseCreator(this);
        return new HttpWebRequestResponseData(response);
    }

    public void WriteToRequestBody(
        Stream requestContent, Stream contentStream, IDictionary<string, string> contentHeaders, IRequestContext requestContext)
    {
    }

    public void WriteToRequestBody(Stream requestContent, byte[] content, IDictionary<string, string> contentHeaders)
    {
    }

    public void Abort()
    {
        this.IsAborted = true;
    }

    public Task<Stream> GetRequestContentAsync()
    {
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public Task<IWebResponseData> GetResponseAsync(CancellationToken cancellationToken)
    {
        this.GetResponseAction?.Invoke();

        var response = this.ResponseCreator(this);
        return Task.FromResult<IWebResponseData>(new HttpWebRequestResponseData(response));
    }

    public void Dispose()
    {
        this.IsDisposed = true;
    }

    public Stream SetupProgressListeners(Stream originalStream, long progressUpdateInterval, object sender, EventHandler<StreamTransferProgressArgs> callback)
    {
        return originalStream;
    }

    Task<Stream> IHttpRequest<Stream>.GetRequestContentAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    Task IHttpRequest<Stream>.WriteToRequestBodyAsync(Stream requestContent, Stream contentStream, IDictionary<string, string> contentHeaders, IRequestContext requestContext)
    {
        throw new NotImplementedException();
    }

    Task IHttpRequest<Stream>.WriteToRequestBodyAsync(Stream requestContent, byte[] requestData, IDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private HttpWebResponse CreateResponse(MockHttpRequest request)
    {
        var resourceName = request.RequestUri.Host.Split('.').Last();
        var response = MockWebResponse.CreateFromResource(resourceName);

        if (response?.StatusCode >= HttpStatusCode.OK && response.StatusCode <= (HttpStatusCode)299)
        {
            return response;
        }
        else
        {
            throw new HttpErrorResponseException(new HttpWebRequestResponseData(response));
        }
    }
}
#else
internal class MockHttpRequest : IHttpRequest<HttpContent>
{
    public MockHttpRequest(Uri requestUri, Action? action, Func<MockHttpRequest, HttpResponseMessage>? responseCreator = null)
    {
        this.RequestUri = requestUri;
        this.GetResponseAction = action;
        this.ResponseCreator = responseCreator ?? this.CreateResponse;
    }

    public bool IsDisposed { get; set; }

    public bool IsAborted { get; set; }

    public bool IsConfigureRequestCalled { get; set; }

    public bool IsSetRequestHeadersCalled { get; set; }

    public bool IsGetRequestContentCalled { get; set; }

    public string? Method { get; set; }

    public Uri RequestUri { get; set; }

    public Action? GetResponseAction { get; set; }

    public Func<MockHttpRequest, HttpResponseMessage> ResponseCreator { get; set; }

    public void Abort()
    {
        this.IsAborted = true;
    }

    public void ConfigureRequest(IRequestContext requestContext)
    {
        this.IsConfigureRequestCalled = true;
    }

    public void Dispose()
    {
        this.IsDisposed = true;
    }

    public HttpContent GetRequestContent()
    {
        this.IsGetRequestContentCalled = true;
        try
        {
            return new HttpRequestMessage().Content!;
        }
        catch (AggregateException e)
        {
            throw e.InnerException ?? e;
        }
    }

    public Task<HttpContent> GetRequestContentAsync()
    {
        return Task.FromResult(new HttpRequestMessage().Content!);
    }

    public IWebResponseData GetResponse()
    {
        this.GetResponseAction?.Invoke();

        var response = this.ResponseCreator(this);
        return CustomWebResponse.GenerateWebResponse(response);
    }

    public Task<IWebResponseData> GetResponseAsync(CancellationToken cancellationToken)
    {
        this.GetResponseAction?.Invoke();
        var response = this.ResponseCreator(this);
        return Task.FromResult(CustomWebResponse.GenerateWebResponse(response));
    }

    public void SetRequestHeaders(IDictionary<string, string> headers)
    {
        this.IsSetRequestHeadersCalled = true;
    }

    public Stream SetupProgressListeners(Stream originalStream, long progressUpdateInterval, object sender, EventHandler<StreamTransferProgressArgs> callback)
    {
        return originalStream;
    }

    public void WriteToRequestBody(HttpContent requestContent, Stream contentStream, IDictionary<string, string> contentHeaders, IRequestContext requestContext)
    {
    }

    public void WriteToRequestBody(HttpContent requestContent, byte[] content, IDictionary<string, string> contentHeaders)
    {
    }

    private HttpResponseMessage CreateResponse(MockHttpRequest request)
    {
        // Extract the last segment of the URI, this is the custom URI
        // sent by the unit tests.
        var resourceName = request.RequestUri.Host.Split('.').Last();
        var response = MockWebResponse.CreateFromResource(resourceName);

        if (response.StatusCode >= HttpStatusCode.OK && response.StatusCode <= (HttpStatusCode)299)
        {
            return response;
        }
        else
        {
            throw new HttpErrorResponseException(CustomWebResponse.GenerateWebResponse(response));
        }
    }
}
#endif
