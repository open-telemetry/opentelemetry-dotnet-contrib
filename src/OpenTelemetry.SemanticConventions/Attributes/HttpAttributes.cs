// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class HttpAttributes
{
    /// <summary>
    /// Deprecated, use <c>client.address</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>client.address</c>")]
    public const string AttributeHttpClientIp = "http.client_ip";

    /// <summary>
    /// State of the HTTP connection in the HTTP connection pool
    /// </summary>
    public const string AttributeHttpConnectionState = "http.connection.state";

    /// <summary>
    /// Deprecated, use <c>network.protocol.name</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>network.protocol.name</c>")]
    public const string AttributeHttpFlavor = "http.flavor";

    /// <summary>
    /// Deprecated, use one of <c>server.address</c>, <c>client.address</c> or <c>http.request.header.host</c> instead, depending on the usage
    /// </summary>
    [Obsolete("Replaced by one of <c>server.address</c>, <c>client.address</c> or <c>http.request.header.host</c>, depending on the usage")]
    public const string AttributeHttpHost = "http.host";

    /// <summary>
    /// Deprecated, use <c>http.request.method</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>http.request.method</c>")]
    public const string AttributeHttpMethod = "http.method";

    /// <summary>
    /// The size of the request payload body in bytes. This is the number of bytes transferred excluding headers and is often, but not always, present as the <a href="https://www.rfc-editor.org/rfc/rfc9110.html#field.content-length">Content-Length</a> header. For requests using transport encoding, this should be the compressed size
    /// </summary>
    public const string AttributeHttpRequestBodySize = "http.request.body.size";

    /// <summary>
    /// HTTP request headers, <c><key></c> being the normalized HTTP Header name (lowercase), the value being the header values
    /// </summary>
    /// <remarks>
    /// Instrumentations SHOULD require an explicit configuration of which headers are to be captured. Including all request headers can be a security risk - explicit configuration helps avoid leaking sensitive information.
    /// The <c>User-Agent</c> header is already captured in the <c>user_agent.original</c> attribute. Users MAY explicitly configure instrumentations to capture them even though it is not recommended.
    /// The attribute value MUST consist of either multiple header values as an array of strings or a single-item array containing a possibly comma-concatenated string, depending on the way the HTTP library provides access to headers
    /// </remarks>
    public const string AttributeHttpRequestHeaderTemplate = "http.request.header";

    /// <summary>
    /// HTTP request method
    /// </summary>
    /// <remarks>
    /// HTTP request method value SHOULD be "known" to the instrumentation.
    /// By default, this convention defines "known" methods as the ones listed in <a href="https://www.rfc-editor.org/rfc/rfc9110.html#name-methods">RFC9110</a>
    /// and the PATCH method defined in <a href="https://www.rfc-editor.org/rfc/rfc5789.html">RFC5789</a>.
    /// <p>
    /// If the HTTP request method is not known to instrumentation, it MUST set the <c>http.request.method</c> attribute to <c>_OTHER</c>.
    /// <p>
    /// If the HTTP instrumentation could end up converting valid HTTP request methods to <c>_OTHER</c>, then it MUST provide a way to override
    /// the list of known HTTP methods. If this override is done via environment variable, then the environment variable MUST be named
    /// OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS and support a comma-separated list of case-sensitive known HTTP methods
    /// (this list MUST be a full override of the default known method, it is not a list of known methods in addition to the defaults).
    /// <p>
    /// HTTP method names are case-sensitive and <c>http.request.method</c> attribute value MUST match a known HTTP method name exactly.
    /// Instrumentations for specific web frameworks that consider HTTP methods to be case insensitive, SHOULD populate a canonical equivalent.
    /// Tracing instrumentations that do so, MUST also set <c>http.request.method_original</c> to the original value
    /// </remarks>
    public const string AttributeHttpRequestMethod = "http.request.method";

    /// <summary>
    /// Original HTTP method sent by the client in the request line
    /// </summary>
    public const string AttributeHttpRequestMethodOriginal = "http.request.method_original";

    /// <summary>
    /// The ordinal number of request resending attempt (for any reason, including redirects)
    /// </summary>
    /// <remarks>
    /// The resend count SHOULD be updated each time an HTTP request gets resent by the client, regardless of what was the cause of the resending (e.g. redirection, authorization failure, 503 Server Unavailable, network issues, or any other)
    /// </remarks>
    public const string AttributeHttpRequestResendCount = "http.request.resend_count";

    /// <summary>
    /// The total size of the request in bytes. This should be the total number of bytes sent over the wire, including the request line (HTTP/1.1), framing (HTTP/2 and HTTP/3), headers, and request body if any
    /// </summary>
    public const string AttributeHttpRequestSize = "http.request.size";

    /// <summary>
    /// Deprecated, use <c>http.request.header.content-length</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>http.request.header.content-length</c>")]
    public const string AttributeHttpRequestContentLength = "http.request_content_length";

    /// <summary>
    /// Deprecated, use <c>http.request.body.size</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>http.request.body.size</c>")]
    public const string AttributeHttpRequestContentLengthUncompressed = "http.request_content_length_uncompressed";

    /// <summary>
    /// The size of the response payload body in bytes. This is the number of bytes transferred excluding headers and is often, but not always, present as the <a href="https://www.rfc-editor.org/rfc/rfc9110.html#field.content-length">Content-Length</a> header. For requests using transport encoding, this should be the compressed size
    /// </summary>
    public const string AttributeHttpResponseBodySize = "http.response.body.size";

    /// <summary>
    /// HTTP response headers, <c><key></c> being the normalized HTTP Header name (lowercase), the value being the header values
    /// </summary>
    /// <remarks>
    /// Instrumentations SHOULD require an explicit configuration of which headers are to be captured. Including all response headers can be a security risk - explicit configuration helps avoid leaking sensitive information.
    /// Users MAY explicitly configure instrumentations to capture them even though it is not recommended.
    /// The attribute value MUST consist of either multiple header values as an array of strings or a single-item array containing a possibly comma-concatenated string, depending on the way the HTTP library provides access to headers
    /// </remarks>
    public const string AttributeHttpResponseHeaderTemplate = "http.response.header";

    /// <summary>
    /// The total size of the response in bytes. This should be the total number of bytes sent over the wire, including the status line (HTTP/1.1), framing (HTTP/2 and HTTP/3), headers, and response body and trailers if any
    /// </summary>
    public const string AttributeHttpResponseSize = "http.response.size";

    /// <summary>
    /// <a href="https://tools.ietf.org/html/rfc7231#section-6">HTTP response status code</a>
    /// </summary>
    public const string AttributeHttpResponseStatusCode = "http.response.status_code";

    /// <summary>
    /// Deprecated, use <c>http.response.header.content-length</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>http.response.header.content-length</c>")]
    public const string AttributeHttpResponseContentLength = "http.response_content_length";

    /// <summary>
    /// Deprecated, use <c>http.response.body.size</c> instead
    /// </summary>
    [Obsolete("Replace by <c>http.response.body.size</c>")]
    public const string AttributeHttpResponseContentLengthUncompressed = "http.response_content_length_uncompressed";

    /// <summary>
    /// The matched route, that is, the path template in the format used by the respective server framework
    /// </summary>
    /// <remarks>
    /// MUST NOT be populated when this is not supported by the HTTP server framework as the route attribute should have low-cardinality and the URI path can NOT substitute it.
    /// SHOULD include the <a href="/docs/http/http-spans.md#http-server-definitions">application root</a> if there is one
    /// </remarks>
    public const string AttributeHttpRoute = "http.route";

    /// <summary>
    /// Deprecated, use <c>url.scheme</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>url.scheme</c> instead")]
    public const string AttributeHttpScheme = "http.scheme";

    /// <summary>
    /// Deprecated, use <c>server.address</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>server.address</c>")]
    public const string AttributeHttpServerName = "http.server_name";

    /// <summary>
    /// Deprecated, use <c>http.response.status_code</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>http.response.status_code</c>")]
    public const string AttributeHttpStatusCode = "http.status_code";

    /// <summary>
    /// Deprecated, use <c>url.path</c> and <c>url.query</c> instead
    /// </summary>
    [Obsolete("Split to <c>url.path</c> and `url.query")]
    public const string AttributeHttpTarget = "http.target";

    /// <summary>
    /// Deprecated, use <c>url.full</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>url.full</c>")]
    public const string AttributeHttpUrl = "http.url";

    /// <summary>
    /// Deprecated, use <c>user_agent.original</c> instead
    /// </summary>
    [Obsolete("Replaced by <c>user_agent.original</c>")]
    public const string AttributeHttpUserAgent = "http.user_agent";

    /// <summary>
    /// State of the HTTP connection in the HTTP connection pool
    /// </summary>
    public static class HttpConnectionStateValues
    {
        /// <summary>
        /// active state
        /// </summary>
        public const string Active = "active";

        /// <summary>
        /// idle state
        /// </summary>
        public const string Idle = "idle";
    }

    /// <summary>
    /// Deprecated, use <c>network.protocol.name</c> instead
    /// </summary>
    public static class HttpFlavorValues
    {
        /// <summary>
        /// HTTP/1.0
        /// </summary>
        [Obsolete("Replaced by <c>network.protocol.name</c>")]
        public const string Http10 = "1.0";

        /// <summary>
        /// HTTP/1.1
        /// </summary>
        [Obsolete("Replaced by <c>network.protocol.name</c>")]
        public const string Http11 = "1.1";

        /// <summary>
        /// HTTP/2
        /// </summary>
        [Obsolete("Replaced by <c>network.protocol.name</c>")]
        public const string Http20 = "2.0";

        /// <summary>
        /// HTTP/3
        /// </summary>
        [Obsolete("Replaced by <c>network.protocol.name</c>")]
        public const string Http30 = "3.0";

        /// <summary>
        /// SPDY protocol
        /// </summary>
        [Obsolete("Replaced by <c>network.protocol.name</c>")]
        public const string Spdy = "SPDY";

        /// <summary>
        /// QUIC protocol
        /// </summary>
        [Obsolete("Replaced by <c>network.protocol.name</c>")]
        public const string Quic = "QUIC";
    }

    /// <summary>
    /// HTTP request method
    /// </summary>
    public static class HttpRequestMethodValues
    {
        /// <summary>
        /// CONNECT method
        /// </summary>
        public const string Connect = "CONNECT";

        /// <summary>
        /// DELETE method
        /// </summary>
        public const string Delete = "DELETE";

        /// <summary>
        /// GET method
        /// </summary>
        public const string Get = "GET";

        /// <summary>
        /// HEAD method
        /// </summary>
        public const string Head = "HEAD";

        /// <summary>
        /// OPTIONS method
        /// </summary>
        public const string Options = "OPTIONS";

        /// <summary>
        /// PATCH method
        /// </summary>
        public const string Patch = "PATCH";

        /// <summary>
        /// POST method
        /// </summary>
        public const string Post = "POST";

        /// <summary>
        /// PUT method
        /// </summary>
        public const string Put = "PUT";

        /// <summary>
        /// TRACE method
        /// </summary>
        public const string Trace = "TRACE";

        /// <summary>
        /// Any HTTP method that the instrumentation has no prior knowledge of
        /// </summary>
        public const string Other = "_OTHER";
    }
}
