// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class UrlAttributes
{
    /// <summary>
    /// Domain extracted from the <c>url.full</c>, such as "opentelemetry.io"
    /// </summary>
    /// <remarks>
    /// In some cases a URL may refer to an IP and/or port directly, without a domain name. In this case, the IP address would go to the domain field. If the URL contains a <a href="https://www.rfc-editor.org/rfc/rfc2732#section-2">literal IPv6 address</a> enclosed by <c>[</c> and <c>]</c>, the <c>[</c> and <c>]</c> characters should also be captured in the domain field
    /// </remarks>
    public const string AttributeUrlDomain = "url.domain";

    /// <summary>
    /// The file extension extracted from the <c>url.full</c>, excluding the leading dot
    /// </summary>
    /// <remarks>
    /// The file extension is only set if it exists, as not every url has a file extension. When the file name has multiple extensions <c>example.tar.gz</c>, only the last one should be captured <c>gz</c>, not <c>tar.gz</c>
    /// </remarks>
    public const string AttributeUrlExtension = "url.extension";

    /// <summary>
    /// The <a href="https://www.rfc-editor.org/rfc/rfc3986#section-3.5">URI fragment</a> component
    /// </summary>
    public const string AttributeUrlFragment = "url.fragment";

    /// <summary>
    /// Absolute URL describing a network resource according to <a href="https://www.rfc-editor.org/rfc/rfc3986">RFC3986</a>
    /// </summary>
    /// <remarks>
    /// For network calls, URL usually has <c>scheme://host[:port][path][?query][#fragment]</c> format, where the fragment is not transmitted over HTTP, but if it is known, it SHOULD be included nevertheless.
    /// <c>url.full</c> MUST NOT contain credentials passed via URL in form of <c>https://username:password@www.example.com/</c>. In such case username and password SHOULD be redacted and attribute's value SHOULD be <c>https://REDACTED:REDACTED@www.example.com/</c>.
    /// <c>url.full</c> SHOULD capture the absolute URL when it is available (or can be reconstructed). Sensitive content provided in <c>url.full</c> SHOULD be scrubbed when instrumentations can identify it
    /// </remarks>
    public const string AttributeUrlFull = "url.full";

    /// <summary>
    /// Unmodified original URL as seen in the event source
    /// </summary>
    /// <remarks>
    /// In network monitoring, the observed URL may be a full URL, whereas in access logs, the URL is often just represented as a path. This field is meant to represent the URL as it was observed, complete or not.
    /// <c>url.original</c> might contain credentials passed via URL in form of <c>https://username:password@www.example.com/</c>. In such case password and username SHOULD NOT be redacted and attribute's value SHOULD remain the same
    /// </remarks>
    public const string AttributeUrlOriginal = "url.original";

    /// <summary>
    /// The <a href="https://www.rfc-editor.org/rfc/rfc3986#section-3.3">URI path</a> component
    /// </summary>
    /// <remarks>
    /// Sensitive content provided in <c>url.path</c> SHOULD be scrubbed when instrumentations can identify it
    /// </remarks>
    public const string AttributeUrlPath = "url.path";

    /// <summary>
    /// Port extracted from the <c>url.full</c>
    /// </summary>
    public const string AttributeUrlPort = "url.port";

    /// <summary>
    /// The <a href="https://www.rfc-editor.org/rfc/rfc3986#section-3.4">URI query</a> component
    /// </summary>
    /// <remarks>
    /// Sensitive content provided in <c>url.query</c> SHOULD be scrubbed when instrumentations can identify it
    /// </remarks>
    public const string AttributeUrlQuery = "url.query";

    /// <summary>
    /// The highest registered url domain, stripped of the subdomain
    /// </summary>
    /// <remarks>
    /// This value can be determined precisely with the <a href="http://publicsuffix.org">public suffix list</a>. For example, the registered domain for <c>foo.example.com</c> is <c>example.com</c>. Trying to approximate this by simply taking the last two labels will not work well for TLDs such as <c>co.uk</c>
    /// </remarks>
    public const string AttributeUrlRegisteredDomain = "url.registered_domain";

    /// <summary>
    /// The <a href="https://www.rfc-editor.org/rfc/rfc3986#section-3.1">URI scheme</a> component identifying the used protocol
    /// </summary>
    public const string AttributeUrlScheme = "url.scheme";

    /// <summary>
    /// The subdomain portion of a fully qualified domain name includes all of the names except the host name under the registered_domain. In a partially qualified domain, or if the qualification level of the full name cannot be determined, subdomain contains all of the names below the registered domain
    /// </summary>
    /// <remarks>
    /// The subdomain portion of <c>www.east.mydomain.co.uk</c> is <c>east</c>. If the domain has multiple levels of subdomain, such as <c>sub2.sub1.example.com</c>, the subdomain field should contain <c>sub2.sub1</c>, with no trailing period
    /// </remarks>
    public const string AttributeUrlSubdomain = "url.subdomain";

    /// <summary>
    /// The low-cardinality template of an <a href="https://www.rfc-editor.org/rfc/rfc3986#section-4.2">absolute path reference</a>
    /// </summary>
    public const string AttributeUrlTemplate = "url.template";

    /// <summary>
    /// The effective top level domain (eTLD), also known as the domain suffix, is the last part of the domain name. For example, the top level domain for example.com is <c>com</c>
    /// </summary>
    /// <remarks>
    /// This value can be determined precisely with the <a href="http://publicsuffix.org">public suffix list</a>
    /// </remarks>
    public const string AttributeUrlTopLevelDomain = "url.top_level_domain";
}
