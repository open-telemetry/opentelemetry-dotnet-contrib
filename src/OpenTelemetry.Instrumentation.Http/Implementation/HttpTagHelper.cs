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

            // Redaction only rewrites the query when it contains a '=', so avoid
            // recreating a string that is equivalent to the original string otherwise.
            // Non HTTP(S) schemes with no authority behave slightly differently
            // for AbsoluteUri so they ignore this optimization to ensure they
            // return the right value to the caller.
            var scheme = uri.Scheme;

            if (scheme == Uri.UriSchemeHttps || scheme == Uri.UriSchemeHttp)
            {
                var indexOfEquals =
#if NET
                    uri.Query.IndexOf('=', StringComparison.Ordinal);
#else
                    uri.Query.IndexOf('=');
#endif

                if (indexOfEquals < 0)
                {
                    return uri.AbsoluteUri;
                }
            }
        }

        var query = disableQueryRedaction ? uri.Query : RedactionHelper.GetRedactedQueryString(uri.Query);

        return string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.AbsolutePath, query, uri.Fragment);
    }
}
