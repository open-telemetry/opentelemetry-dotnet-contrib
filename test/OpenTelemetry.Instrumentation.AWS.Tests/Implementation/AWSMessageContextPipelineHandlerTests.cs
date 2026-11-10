// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Runtime;
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

    [Theory]
    [InlineData(true, 0, false, true)]
    [InlineData(false, 0, false, true)]
    [InlineData(true, 9, false, true)] // 9 + traceparent = 10
    [InlineData(false, 9, false, true)]
    [InlineData(true, 10, false, false)] // 10 + traceparent = 11
    [InlineData(false, 10, false, false)]
    [InlineData(true, 8, true, true)] // 8 + traceparent + baggage = 10
    [InlineData(false, 8, true, true)]
    [InlineData(true, 9, true, false)] // 9 + traceparent + baggage = 11
    [InlineData(false, 9, true, false)]
    public void TryGetSendContext_NoApplicationContext_CanInjectDependsOnAttributes(bool isSqs, int attributes, bool hasBaggage, bool expected)
    {
        var previousBaggage = Baggage.Current;

        try
        {
            if (hasBaggage)
            {
                Baggage.Current = Baggage.Create(new Dictionary<string, string> { ["key"] = "value" });
            }

            var request = CreateRequest(isSqs, Enumerable.Range(0, attributes).ToDictionary(i => $"application-{i}", _ => "value"));

            Assert.True(AWSMessageContextPipelineHandler.TryGetSendContext(request, out var sendContext));
            Assert.Equal(default, sendContext.CreationContext);
            Assert.Equal(expected, sendContext.CanInjectContext);
        }
        finally
        {
            Baggage.Current = previousBaggage;
        }
    }

    [Theory]
    [InlineData(true, 0, null, true)]
    [InlineData(false, 0, null, true)]
    [InlineData(true, 8, null, true)] // 8 + traceparent + tracestate = 10
    [InlineData(false, 8, null, true)]
    [InlineData(true, 9, null, false)] // 9 + traceparent + tracestate = 11
    [InlineData(false, 9, null, false)]
    [InlineData(true, 0, "tracestate", false)] // The application's own tracestate would be replaced
    [InlineData(false, 0, "tracestate", false)]
    public void TryGetSendContext_ParentWithTraceState_TraceStateIsInjected(bool isSqs, int attributes, string? additionalAttribute, bool expected)
    {
        using var parent = new Activity("parent");
        parent.TraceStateString = TraceState;
        parent.Start();

        try
        {
            var values = Enumerable.Range(0, attributes).ToDictionary(i => $"application-{i}", _ => "value");

            if (additionalAttribute is not null)
            {
                values[additionalAttribute] = "value";
            }

            Assert.True(AWSMessageContextPipelineHandler.TryGetSendContext(CreateRequest(isSqs, values), out var sendContext));
            Assert.Equal(default, sendContext.CreationContext);
            Assert.Equal(expected, sendContext.CanInjectContext);
        }
        finally
        {
            parent.Stop();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetSendContext_NoMessageAttributes_CanInjectContext(bool isSqs)
    {
        Assert.True(AWSMessageContextPipelineHandler.TryGetSendContext(CreateRequest(isSqs, null), out var sendContext));
        Assert.Equal(default, sendContext.CreationContext);
        Assert.True(sendContext.CanInjectContext);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetSendContext_ApplicationContext_IsCreationContextAndNotInjected(bool isSqs)
    {
        var request = CreateRequest(isSqs, new Dictionary<string, string> { ["traceparent"] = TraceParent, ["tracestate"] = TraceState });

        Assert.True(AWSMessageContextPipelineHandler.TryGetSendContext(request, out var sendContext));
        Assert.True(ActivityContext.TryParse(TraceParent, TraceState, isRemote: true, out var expected));
        Assert.Equal(expected, sendContext.CreationContext);
        Assert.False(sendContext.CanInjectContext);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetSendContext_MalformedApplicationContext_NotACreationContextAndNotInjected(bool isSqs)
    {
        // The context cannot be injected as the application's attribute would be replaced, so the span is not the creation context.
        var request = CreateRequest(isSqs, new Dictionary<string, string> { ["traceparent"] = "not-a-traceparent" });

        Assert.True(AWSMessageContextPipelineHandler.TryGetSendContext(request, out var sendContext));
        Assert.Equal(default, sendContext.CreationContext);
        Assert.False(sendContext.CanInjectContext);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetSendContext_ContextInjectedByEarlierSend_IsNotCreationContextAndCanBeReplaced(bool isSqs)
    {
        var request = CreateRequest(isSqs, null);
        var propagated = new Dictionary<string, string> { ["traceparent"] = TraceParent, ["tracestate"] = TraceState };

        if (isSqs)
        {
            SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), propagated);
        }
        else
        {
            SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), propagated);
        }

        Assert.True(AWSMessageContextPipelineHandler.TryGetSendContext(request, out var sendContext));
        Assert.Equal(default, sendContext.CreationContext);
        Assert.True(sendContext.CanInjectContext);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetSendContext_FailureWhileInspectingAttributes_DoesNotThrowAndCannotInject(bool isSqs)
    {
        var comparer = new FailingComparer();

        AmazonWebServiceRequest request;

        if (isSqs)
        {
            request = new SQS.SendMessageRequest
            {
                MessageAttributes = new Dictionary<string, SQS.MessageAttributeValue>(comparer)
                {
                    ["application"] = new() { DataType = "String", StringValue = "value" },
                },
            };
        }
        else
        {
            request = new SNS.PublishRequest
            {
                MessageAttributes = new Dictionary<string, SNS.MessageAttributeValue>(comparer)
                {
                    ["application"] = new() { DataType = "String", StringValue = "value" },
                },
            };
        }

        comparer.Fail = true;

        Assert.True(AWSMessageContextPipelineHandler.TryGetSendContext(request, out var sendContext));
        Assert.Equal(default, sendContext.CreationContext);
        Assert.False(sendContext.CanInjectContext);
    }

    [Fact]
    public void TryGetSendContext_OtherRequests_ReturnsFalse()
    {
        Assert.False(AWSMessageContextPipelineHandler.TryGetSendContext(new SQS.ReceiveMessageRequest(), out var sendContext));
        Assert.Equal(default, sendContext.CreationContext);
        Assert.False(sendContext.CanInjectContext);
    }

    private static AmazonWebServiceRequest CreateRequest(bool isSqs, Dictionary<string, string>? attributes)
    {
        if (isSqs)
        {
            return new SQS.SendMessageRequest
            {
                MessageAttributes = attributes?.ToDictionary(a => a.Key, a => new SQS.MessageAttributeValue { DataType = "String", StringValue = a.Value }),
            };
        }

        return new SNS.PublishRequest
        {
            MessageAttributes = attributes?.ToDictionary(a => a.Key, a => new SNS.MessageAttributeValue { DataType = "String", StringValue = a.Value }),
        };
    }

    private sealed class FailingComparer : IEqualityComparer<string>
    {
        public bool Fail { get; set; }

        public bool Equals(string? x, string? y)
            => this.Fail ? ThrowFailure<bool>() : string.Equals(x, y, StringComparison.Ordinal);

        public int GetHashCode(string obj)
            => this.Fail ? ThrowFailure<int>() : StringComparer.Ordinal.GetHashCode(obj);

        private static T ThrowFailure<T>() => throw new InvalidOperationException("Failed.");
    }
}
