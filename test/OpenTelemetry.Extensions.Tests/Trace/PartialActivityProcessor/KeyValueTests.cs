// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.Trace.PartialProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class KeyValueTests
{
    [Fact]
    public void KeyValue_ShouldSetAndGetPropertiesCorrectly()
    {
        var keyValue = new KeyValue();
        var expectedKey = "TestKey";
        var expectedValue = new AnyValue("TestValue");

        keyValue.Key = expectedKey;
        keyValue.Value = expectedValue;

        Assert.Equal(expectedKey, keyValue.Key);
        Assert.Equal(expectedValue, keyValue.Value);
    }

    [Fact]
    public void KeyValue_ShouldHandleNullValues()
    {
        var keyValue = new KeyValue();

        keyValue.Key = null;
        keyValue.Value = null;

        Assert.Null(keyValue.Key);
        Assert.Null(keyValue.Value);
    }
}
