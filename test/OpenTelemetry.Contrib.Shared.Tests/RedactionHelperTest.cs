// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal.Tests;

public class RedactionHelperTest
{
    [Theory]
    [InlineData("?a", "?a")]
    [InlineData("?a=b", "?a=Redacted")]
    [InlineData("?a=b&", "?a=Redacted&")]
    [InlineData("?c=b&", "?c=Redacted&")]
    [InlineData("?c=a", "?c=Redacted")]
    [InlineData("?a=b&c", "?a=Redacted&c")]
    [InlineData("?a=b&c=1&", "?a=Redacted&c=Redacted&")]
    [InlineData("?a=b&c=1&a1", "?a=Redacted&c=Redacted&a1")]
    [InlineData("?a=b&c=1&a1=", "?a=Redacted&c=Redacted&a1=Redacted")]
    [InlineData("?a=b&c=11&a1=&", "?a=Redacted&c=Redacted&a1=Redacted&")]
    [InlineData("?c&c&c&", "?c&c&c&")]
    [InlineData("?a&a&a&a", "?a&a&a&a")]
    [InlineData("?&&&&&&&", "?&&&&&&&")]
    [InlineData("?c", "?c")]
    [InlineData("?=c", "?=Redacted")]
    [InlineData("?=c&=", "?=Redacted&=Redacted")]
    public void QueryStringIsRedacted(string input, string expected)
        => Assert.Equal(expected, RedactionHelper.GetRedactedQueryString(input));

    [Theory]
    [InlineData("https://h/p?a=b", 11, 15, "https://h/p?a=Redacted")]
    [InlineData("https://h/p?a=b#c=d&e", 11, 15, "https://h/p?a=Redacted#c=d&e")]
    [InlineData("a=b&c?d=e&f#g=h&i", 5, 11, "a=b&c?d=Redacted&f#g=h&i")]
    [InlineData("=&?=&#=&", 2, 5, "=&?=Redacted&#=&")]
    [InlineData("?a=b", 0, 4, "?a=Redacted")]
    [InlineData("?a=b", 0, 3, "?a=Redactedb")]
    [InlineData("x?a=bcd&e=f", 1, 11, "x?a=Redacted&e=Redacted")]
    public void QueryStringInValueIsRedacted(string value, int queryStart, int queryEnd, string expected)
        => Assert.Equal(expected, RedactionHelper.GetRedactedQueryString(value, queryStart, queryEnd));

    [Theory]
    [InlineData("https://h/p", 11, 11)]
    [InlineData("https://h/p?", 11, 12)]
    [InlineData("https://h/p?a&b", 11, 15)]
    [InlineData("a=b?c&d#e=f", 3, 7)]
    [InlineData("", 0, 0)]
    public void ValueIsReturnedIfQueryStringHasNothingToRedact(string value, int queryStart, int queryEnd)
        => Assert.Same(value, RedactionHelper.GetRedactedQueryString(value, queryStart, queryEnd));

    [Fact]
    public void QueryStringInValueIsRedactedLikeQueryString()
    {
        const string Alphabet = "ab=&?#";

        var random = new Random(20261002);

        for (var i = 0; i < 20_000; i++)
        {
            var prefix = CreateRandomString(random, Alphabet, 6);
            var query = CreateRandomString(random, Alphabet, 16);
            var suffix = CreateRandomString(random, Alphabet, 6);

            var value = prefix + query + suffix;

            var expected = prefix + RedactQueryString(query) + suffix;
            var actual = RedactionHelper.GetRedactedQueryString(value, prefix.Length, prefix.Length + query.Length);

            Assert.Equal(expected, actual);
            Assert.Equal(RedactQueryString(query), RedactionHelper.GetRedactedQueryString(query));
        }
    }

    private static string CreateRandomString(Random random, string alphabet, int maxLength)
    {
        var chars = new char[random.Next(maxLength + 1)];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[random.Next(alphabet.Length)];
        }

        return new string(chars);
    }

    /// <summary>
    /// A reference implementation of query string redaction that replaces the value
    /// of each key/value pair (separated by '&amp;') that contains a '=' with "Redacted".
    /// </summary>
    private static string RedactQueryString(string query)
    {
        var pairs = query.Split('&');

        for (var i = 0; i < pairs.Length; i++)
        {
            var index = pairs[i].AsSpan().IndexOf('=');

            if (index >= 0)
            {
                pairs[i] = pairs[i].AsSpan(0, index + 1).ToString() + "Redacted";
            }
        }

        return string.Join("&", pairs);
    }
}
