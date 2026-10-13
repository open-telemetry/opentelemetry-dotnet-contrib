// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Http.Implementation;

/// <summary>
/// A collection of helper methods to be used when building Http activities.
/// </summary>
internal static class HttpTagHelper
{
    internal static readonly RequestDataHelper RequestDataHelper = new(configureByHttpKnownMethodsEnvironmentalVariable: false);

    /// <summary>
    /// Gets the OpenTelemetry standard uri tag value for a span based on its request <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri"><see cref="Uri"/>.</param>
    /// <param name="disableQueryRedaction">Indicates whether query parameter should be redacted or not.</param>
    /// <returns>Span uri value.</returns>
    public static string GetUriTagValueFromRequestUri(Uri uri, bool disableQueryRedaction)
    {
        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            if (disableQueryRedaction)
            {
                return uri.OriginalString;
            }

            // Non HTTP(S) schemes with no authority behave slightly differently
            // for AbsoluteUri so they ignore this optimization to ensure they
            // return the right value to the caller.
            var scheme = uri.Scheme;

            if (scheme == Uri.UriSchemeHttps || scheme == Uri.UriSchemeHttp)
            {
                // For HTTP(S) URIs with no user information, the AbsoluteUri (which is cached
                // by Uri) is the concatenation of the scheme, "://", the authority, the path,
                // the query and the fragment. This allows the query to be redacted in place
                // without allocating each of the components of the URI as a new string.
                var absoluteUri = uri.AbsoluteUri;
                var delimiter = absoluteUri.AsSpan().IndexOfAny('?', '#');

                if (delimiter < 0)
                {
                    // There is no query or fragment.
                    return absoluteUri;
                }

                var queryStart = -1;
                var queryEnd = delimiter;

                if (absoluteUri[delimiter] == '?')
                {
                    queryStart = delimiter;

                    var fragmentStart = absoluteUri.AsSpan(queryStart).IndexOf('#');
                    queryEnd = fragmentStart < 0 ? absoluteUri.Length : queryStart + fragmentStart;
                }

                if (queryEnd < absoluteUri.Length && uri.Fragment.Length != absoluteUri.Length - queryEnd)
                {
                    // The '#' does not start the fragment, which happens if the URI was created with
                    // UriCreationOptions.DangerousDisablePathAndQueryCanonicalization set to true.
                    var query = uri.Query;

                    return query.AsSpan().IndexOf('=') < 0
                        ? absoluteUri
                        : ConcatUriComponents(uri, RedactionHelper.GetRedactedQueryString(query));
                }

                // Redaction only rewrites the query when it contains a '=', so the original
                // string is returned if there is no query or it contains nothing to redact.
                return queryStart < 0
                    ? absoluteUri
                    : RedactionHelper.GetRedactedQueryString(absoluteUri, queryStart, queryEnd);
            }
        }

        var uriQuery = uri.Query;

        if (!disableQueryRedaction)
        {
            uriQuery = RedactionHelper.GetRedactedQueryString(uriQuery);
        }

        return ConcatUriComponents(uri, uriQuery);
    }

    private static string ConcatUriComponents(Uri uri, string? query)
        => string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.AbsolutePath, query, uri.Fragment);
}
