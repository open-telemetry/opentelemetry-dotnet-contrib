// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class HttpTagHelperTests
{
    [Fact]
    public void DisableQueryRedactionTakesPrecedenceOverSensitiveQueryParameters()
    {
        var uri = new Uri("http://example.com/p?a=b&sig=ghgjgj");
        string[] sensitiveQueryParameters = ["sig"];

        Assert.Equal(uri.OriginalString, HttpTagHelper.GetUriTagValueFromRequestUri(uri, disableQueryRedaction: true, sensitiveQueryParameters));
    }
}
