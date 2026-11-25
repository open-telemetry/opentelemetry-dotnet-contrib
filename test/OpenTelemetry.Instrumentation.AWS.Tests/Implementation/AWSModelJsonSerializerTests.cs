// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using OpenTelemetry.Instrumentation.AWS.Implementation;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSModelJsonSerializerTests
{
    [Fact]
    public void SerializesConsumedCapacityWithSortedPascalCaseKeys()
    {
        var value = new ConsumedCapacity { TableName = "T", CapacityUnits = 1.5 };

        Assert.Equal("{\"CapacityUnits\":1.5,\"TableName\":\"T\"}", AWSModelJsonSerializer.Serialize(value));
    }

    [Fact]
    public void SkipsNullAndEmptyCollections()
    {
        var value = new GlobalSecondaryIndex { IndexName = "Idx", KeySchema = [] };

        Assert.Equal("{\"IndexName\":\"Idx\"}", AWSModelJsonSerializer.Serialize(value));
    }

    [Fact]
    public void WritesConstantClassAsValue()
    {
        var value = new Projection { ProjectionType = ProjectionType.KEYS_ONLY };

        Assert.Equal("{\"ProjectionType\":\"KEYS_ONLY\"}", AWSModelJsonSerializer.Serialize(value));
    }

    [Fact]
    public void WritesMemoryStreamAsBase64WithoutMovingPosition()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        stream.Position = 2;
        var value = new AttributeValue { B = stream };

        Assert.Equal("{\"B\":\"AQID\"}", AWSModelJsonSerializer.Serialize(value));
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void WritesDictionaryAsObject()
    {
        var value = new ItemCollectionMetrics
        {
            ItemCollectionKey = new Dictionary<string, AttributeValue> { ["pk"] = new() { S = "a" } },
            SizeEstimateRangeGB = [1.0, 2.0],
        };

        Assert.Equal(
            "{\"ItemCollectionKey\":{\"pk\":{\"S\":\"a\"}},\"SizeEstimateRangeGB\":[1,2]}",
            AWSModelJsonSerializer.Serialize(value));
    }

    [Fact]
    public void DoesNotEscapeNonAsciiOrHtmlSensitiveCharacters()
    {
        var value = new AttributeValue { S = "user+1@é.com & <x>" };

        Assert.Equal("{\"S\":\"user+1@é.com & <x>\"}", AWSModelJsonSerializer.Serialize(value));
    }

    [Fact]
    public void ReturnsNullForNullAndEmptyObject()
    {
        Assert.Null(AWSModelJsonSerializer.Serialize(null));
        Assert.Null(AWSModelJsonSerializer.Serialize(new ConsumedCapacity()));
    }
}
