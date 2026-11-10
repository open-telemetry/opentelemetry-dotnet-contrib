// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using SNS = Amazon.SimpleNotificationService.Model;
using SQS = Amazon.SQS.Model;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSMessageContextPipelineHandlerTests
{
    private const string TraceParent = "00-5759e988bd862e3fe1be46a994272793-53995c3f42cd8ad8-01";
    private const string TraceState = "key=value";

    static AWSMessageContextPipelineHandlerTests()
    {
        // The SDK configures the default propagator that is used to extract the context,
        // which is always the case when the instrumentation is in use.
        _ = Sdk.SuppressInstrumentation;
    }

    [Theory]
    [InlineData(SemanticConventionVersion.V1_28_0, false)]
    [InlineData(SemanticConventionVersion.V1_29_0, false)]
    [InlineData(SemanticConventionVersion.V1_40_0, false)]
    [InlineData(SemanticConventionVersion.V1_45_0, true)]
    [InlineData(SemanticConventionVersion.Latest, true)]
    public void UsesProducerSpansForMessageSend_ReturnsExpectedValue(SemanticConventionVersion version, bool expected)
        => Assert.Equal(expected, AWSServiceHelper.UsesProducerSpansForMessageSend(version));

    [Fact]
    public void ExtractCreationContext_SqsAttributesSuppliedByApplication_ReturnsContext()
    {
        var request = new SQS.SendMessageRequest
        {
            MessageAttributes = new Dictionary<string, SQS.MessageAttributeValue>
            {
                ["traceparent"] = new() { DataType = "String", StringValue = TraceParent },
                ["tracestate"] = new() { DataType = "String", StringValue = TraceState },
            },
        };

        var context = AWSMessageContextPipelineHandler.ExtractCreationContext(request);

        Assert.True(ActivityContext.TryParse(TraceParent, TraceState, isRemote: true, out var expected));
        Assert.Equal(expected, context);
    }

    [Fact]
    public void ExtractCreationContext_SnsAttributesSuppliedByApplication_ReturnsContext()
    {
        var request = new SNS.PublishRequest
        {
            MessageAttributes = new Dictionary<string, SNS.MessageAttributeValue>
            {
                ["traceparent"] = new() { DataType = "String", StringValue = TraceParent },
                ["tracestate"] = new() { DataType = "String", StringValue = TraceState },
            },
        };

        var context = AWSMessageContextPipelineHandler.ExtractCreationContext(request);

        Assert.True(ActivityContext.TryParse(TraceParent, TraceState, isRemote: true, out var expected));
        Assert.Equal(expected, context);
    }

    [Fact]
    public void ExtractCreationContext_SqsNullAttributeValues_ReturnsDefault()
    {
        var request = new SQS.SendMessageRequest
        {
            MessageAttributes = new Dictionary<string, SQS.MessageAttributeValue>
            {
                ["traceparent"] = null!,
                ["tracestate"] = new() { DataType = "String", StringValue = null },
            },
        };

        Assert.Equal(default, AWSMessageContextPipelineHandler.ExtractCreationContext(request));
    }

    [Fact]
    public void ExtractCreationContext_SnsNullAttributeValues_ReturnsDefault()
    {
        var request = new SNS.PublishRequest
        {
            MessageAttributes = new Dictionary<string, SNS.MessageAttributeValue>
            {
                ["traceparent"] = null!,
                ["tracestate"] = new() { DataType = "String", StringValue = null },
            },
        };

        Assert.Equal(default, AWSMessageContextPipelineHandler.ExtractCreationContext(request));
    }

    [Fact]
    public void ExtractCreationContext_OtherRequests_ReturnsDefault()
        => Assert.Equal(default, AWSMessageContextPipelineHandler.ExtractCreationContext(new SQS.ReceiveMessageRequest()));
}
