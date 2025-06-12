// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Data;
using Xunit;

namespace OpenTelemetry.OpAMPClient.Tests;

public class AnyValueUnionTests
{
    [Fact]
    public void AnyValueUnion_EqualityTests()
    {
        var intValue1 = new AnyValueUnion(AnyValueType.Integer, intValue: 42);
        var intValue2 = new AnyValueUnion(AnyValueType.Integer, intValue: 42);
        var boolValue = new AnyValueUnion(AnyValueType.Boolean, boolValue: true);
        var stringValue = new AnyValueUnion(AnyValueType.String, stringValue: "test");
        var doubleValue = new AnyValueUnion(AnyValueType.Double, doubleValue: 3.14);

        Assert.Equal(intValue1, intValue2);
        Assert.NotEqual(intValue1, boolValue);
        Assert.NotEqual(intValue1, stringValue);
        Assert.NotEqual(intValue1, doubleValue);
        Assert.NotEqual(boolValue, stringValue);
        Assert.NotEqual(boolValue, doubleValue);
        Assert.NotEqual(stringValue, doubleValue);
    }

    [Fact]
    public void AnyValueUnion_HashCodeTests()
    {
        var intValue = new AnyValueUnion(AnyValueType.Integer, intValue: 42);
        var boolValue = new AnyValueUnion(AnyValueType.Boolean, boolValue: true);
        var stringValue = new AnyValueUnion(AnyValueType.String, stringValue: "test");
        var doubleValue = new AnyValueUnion(AnyValueType.Double, doubleValue: 3.14);
        Assert.Equal(intValue.GetHashCode(), new AnyValueUnion(AnyValueType.Integer, intValue: 42).GetHashCode());
        Assert.Equal(intValue.GetHashCode(), new AnyValueUnion(AnyValueType.Boolean, boolValue: true).GetHashCode());
        Assert.Equal(intValue.GetHashCode(), new AnyValueUnion(AnyValueType.String, stringValue: "test").GetHashCode());
        Assert.Equal(intValue.GetHashCode(), new AnyValueUnion(AnyValueType.Double, doubleValue: 3.14).GetHashCode());
        Assert.NotEqual(intValue.GetHashCode(), boolValue.GetHashCode());
        Assert.NotEqual(intValue.GetHashCode(), stringValue.GetHashCode());
        Assert.NotEqual(intValue.GetHashCode(), doubleValue.GetHashCode());
        Assert.NotEqual(boolValue.GetHashCode(), stringValue.GetHashCode());
        Assert.NotEqual(boolValue.GetHashCode(), doubleValue.GetHashCode());
        Assert.NotEqual(stringValue.GetHashCode(), doubleValue.GetHashCode());
    }
}
