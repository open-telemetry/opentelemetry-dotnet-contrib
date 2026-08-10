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
    {
        Assert.Equal(expected, RedactionHelper.GetRedactedQueryString(input));
    }

    [Theory]
    [InlineData("sig", "?a", "?a")]
    [InlineData("sig", "?a=b", "?a=b")]
    [InlineData("sig", "?sig=ghgjgj", "?sig=REDACTED")]
    [InlineData("sig", "?sig=", "?sig=REDACTED")]
    [InlineData("sig", "?sig=ghgjgj&", "?sig=REDACTED&")]
    [InlineData("sig", "?a=b&sig=ghgjgj", "?a=b&sig=REDACTED")]
    [InlineData("sig", "?sig=ghgjgj&a=b", "?sig=REDACTED&a=b")]
    [InlineData("sig", "?a=b&sig=ghgjgj&c=1123456", "?a=b&sig=REDACTED&c=1123456")]
    [InlineData("sig", "?a&sig=ghgjgj", "?a&sig=REDACTED")]
    [InlineData("sig", "?SIG=ghgjgj", "?SIG=ghgjgj")]
    [InlineData("sig", "?asig=ghgjgj", "?asig=ghgjgj")]
    [InlineData("sig", "?a=sig&c=1123456", "?a=sig&c=1123456")]
    [InlineData("sig", "?sig=ghgjgj==&a=b", "?sig=REDACTED&a=b")]
    [InlineData("sig", "?a=gh=jgj&sig=ghgjgj", "?a=gh=jgj&sig=REDACTED")]
    [InlineData("sig", "?=ghgjgj", "?=ghgjgj")]
    [InlineData("sig,c", "?a=b&sig=ghgjgj&c=1123456", "?a=b&sig=REDACTED&c=REDACTED")]
    [InlineData("a", "?a=b&a=bdjdjh", "?a=REDACTED&a=REDACTED")]
    public void OnlySensitiveQueryStringValuesAreRedacted(string sensitiveQueryParameters, string input, string expected)
    {
        var parameters = sensitiveQueryParameters.Split(',');

        Assert.Equal(expected, RedactionHelper.GetRedactedQueryString(input, parameters));
    }
}
