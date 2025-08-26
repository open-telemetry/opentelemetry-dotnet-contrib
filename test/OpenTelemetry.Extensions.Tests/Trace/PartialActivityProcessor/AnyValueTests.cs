// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.Trace.PartialProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class AnyValueTests
{
    [Fact]
    public void Constructor_ShouldSetStringValue()
    {
        var stringValue = "test";

        var anyValue = new AnyValue(stringValue);

        Assert.Equal(stringValue, anyValue.StringValue);
    }
}
