// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class HttpTagHelperTests
{
    private static readonly string[] Queries =
    [
        string.Empty,
        "?",
        "?=",
        "?a",
        "?a=bdjdjh",
        "?a=b&",
        "?c=b&",
        "?c=a",
        "?a=b&c",
        "?a=b&c=1123456&",
        "?a=b&c=1&a1",
        "?a=ghgjgj&c=1deedd&a1=",
        "?a=b&c=11&a1=&",
        "?c&c&c&",
        "?a&a&a&a",
        "?&&&&&&&",
        "?c",
        "?c=%26&",
        "?a=1&&b=2",
        "?a=b=c&d",
        "?a&b=1&c",
        "?=c&=",
        "?%3D=1&b=%3D",
        "?q=a%20b&r=%26",
        "?a= b",
        "?a=é&b=ü",
        "?a=1?b=2",
        "?api-version=2023-01-01",
        "?query=shoes&page=2&size=50&sort=price",
    ];

    private static readonly string[] Fragments =
    [
        string.Empty,
        "#",
        "#frag",
        "#frag=1&x=2",
        "#frag?x=1",
        "#a#b",
    ];

    private static readonly string[] Urls =
    [
        "https://example.com",
        "https://example.com/",
        "https://example.com/p",
        "https://example.com?a=1",
        "https://example.com/p?flag",
        "https://myaccount.blob.core.windows.net/container/blob?api-version=2023-01-01",
        "https://h:8443/a/b?x=1&y=2&z=3#f",
        "https://h/a/b#frag?x=1",
        "https://h/a/b?x=1#frag?y=2",
        "https://[::1]:5001/p?q=1",
        "http://[fe80::1%25eth0]:8080/p?q=1",
        "http://[::ffff:127.0.0.1]/p?q=1",
        "https://xn--bcher-kva.example/p?q=1",
        "https://bücher.example/p?q=1",
        "https://bücher.example/bücher?bücher=bücher#bücher",
        "http://example.com:80/p?q=1",
        "https://example.com:443/p?q=1",
        "http://example.com:443/p?q=1",
        "HTTPS://EXAMPLE.COM/P?Q=1",
        "https://example.com:443/p%20q/%41?q=a%20b&r=%26",
        "https://example.com/p q/?a=b c",
        "https://example.com/a/../b/./c?d=e",
        "https://example.com/%7Euser/?a=%7E",
        "https://example.com/p%3Fq%23r?a=%23",
        "http://10.0.0.5:8080/x/y/z?token=abc123&sig=xyz",
        "http://localhost:1234/path?a=1",
        "https://user:pass@example.com/p?q=1",
        "https://user@example.com/p?q=1#f",
        "ftp://example.com/p?q=1",
        "ws://example.com/p?q=1",
        "file:///c:/p?q=1",
    ];

    public static TheoryData<string> UrlData()
    {
        var data = new TheoryData<string>();

        foreach (var url in Urls)
        {
            data.Add(url);
        }

        // The query strings used by HttpClientTests.ValidateUrlQueryRedaction, combined with fragments
        foreach (var query in Queries)
        {
            foreach (var fragment in Fragments)
            {
                data.Add($"http://localhost:1234/path{query}{fragment}");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UrlData))]
    public void GetUriTagValueFromRequestUriMatchesUriComponents(string url)
        => AssertEquivalent(() => new Uri(url));

#if NET
    [Theory]
    [MemberData(nameof(UrlData))]
    public void GetUriTagValueFromRequestUriMatchesUriComponentsWithCanonicalizationDisabled(string url)
    {
        // With DangerousDisablePathAndQueryCanonicalization the fragment is not parsed,
        // so any '#' is part of the path or query of the URI.
        var options = new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true };
        AssertEquivalent(() => new Uri(url, options));
    }
#endif

    [Fact]
    public void GetUriTagValueFromRequestUriMatchesUriComponentsForRandomUrls()
    {
        const string Alphabet = "ab1=&?#%/ é";

        var random = new Random(20261002);
        var builder = new StringBuilder();

        for (var i = 0; i < 2_000; i++)
        {
            builder.Clear();
            builder.Append(random.Next(2) == 0 ? "http://example.com" : "https://example.com:8443");
            builder.Append('/');

            var length = random.Next(24);
            for (var j = 0; j < length; j++)
            {
                builder.Append(Alphabet[random.Next(Alphabet.Length)]);
            }

            var url = builder.ToString();
            AssertEquivalent(() => new Uri(url));
        }
    }

    [Theory]
    [InlineData("https://example.com/p", "https://example.com/p")]
    [InlineData("https://example.com/p?a", "https://example.com/p?a")]
    [InlineData("https://example.com/p?a=1&b=2", "https://example.com/p?a=Redacted&b=Redacted")]
    [InlineData("https://example.com/p?a=1#f=2", "https://example.com/p?a=Redacted#f=2")]
    [InlineData("https://example.com/p#f?a=1", "https://example.com/p#f?a=1")]
    [InlineData("https://example.com:443/p?a=1", "https://example.com/p?a=Redacted")]
    [InlineData("https://user:pass@example.com/p?a=1", "https://example.com/p?a=Redacted")]
    public void GetUriTagValueFromRequestUriRedactsQuery(string url, string expected)
    {
        var actual = HttpTagHelper.GetUriTagValueFromRequestUri(new Uri(url), disableQueryRedaction: false);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetUriTagValueFromRequestUriReturnsAbsoluteUriIfNothingToRedact()
    {
        var uri = new Uri("https://example.com/p?a#b");
        var actual = HttpTagHelper.GetUriTagValueFromRequestUri(uri, disableQueryRedaction: false);
        Assert.Same(uri.AbsoluteUri, actual);
    }

    private static void AssertEquivalent(Func<Uri> uriFactory)
    {
        foreach (var disableQueryRedaction in new[] { false, true })
        {
            // Use a new Uri for each implementation so that any lazily computed state is not shared.
            var expected = GetUriTagValueFromUriComponents(uriFactory(), disableQueryRedaction);
            var actual = HttpTagHelper.GetUriTagValueFromRequestUri(uriFactory(), disableQueryRedaction);

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// The previous implementation of <see cref="HttpTagHelper.GetUriTagValueFromRequestUri"/>, which
    /// concatenates the components of the URI with the redacted query.
    /// </summary>
    private static string GetUriTagValueFromUriComponents(Uri uri, bool disableQueryRedaction)
    {
        string? query = null;

        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            if (disableQueryRedaction)
            {
                return uri.OriginalString;
            }

            var scheme = uri.Scheme;

            if (scheme == Uri.UriSchemeHttps || scheme == Uri.UriSchemeHttp)
            {
                query = uri.Query;

                var indexOfEquals =
#if NET
                    query.IndexOf('=', StringComparison.Ordinal);
#else
                    query.IndexOf('=');
#endif

                if (indexOfEquals < 0)
                {
                    return uri.AbsoluteUri;
                }
            }
        }

        query ??= uri.Query;

        if (!disableQueryRedaction)
        {
            query = RedactionHelper.GetRedactedQueryString(query);
        }

        return string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.AbsolutePath, query, uri.Fragment);
    }
}
