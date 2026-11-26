// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2.Model;
using OpenTelemetry.Instrumentation.AWS.Implementation;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSModelJsonSerializerTests
{
    [Fact]
    public void SkipsNullAndEmptyCollections()
    {
        var value = new GlobalSecondaryIndex { IndexName = "Idx", KeySchema = [] };

        Assert.Equal("{\"IndexName\":\"Idx\"}", AWSModelJsonSerializer.Serialize(value));
    }

    [Fact]
    public void WritesMemoryStreamAsBase64()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var value = new AttributeValue { B = stream };

        Assert.Equal("{\"B\":\"AQID\"}", AWSModelJsonSerializer.Serialize(value));
    }

    [Fact]
    public void DoesNotEscapeNonAsciiOrHtmlSensitiveCharacters()
    {
        var value = new AttributeValue { S = "user+1@\u00E9.com & <x>" };

        Assert.Equal("{\"S\":\"user+1@\u00E9.com & <x>\"}", AWSModelJsonSerializer.Serialize(value));
    }

    [Fact]
    public void WritesNullForTypesOutsideTheSdkModel()
        => Assert.Equal(
            "[null,\"a\"]",
            AWSModelJsonSerializer.Serialize(new List<object> { TimeSpan.FromSeconds(1), "a" }));

    [Fact]
    public void ReturnsNullForNullAndEmptyObject()
    {
        Assert.Null(AWSModelJsonSerializer.Serialize(null));
        Assert.Null(AWSModelJsonSerializer.Serialize(new ConsumedCapacity()));
    }
}
