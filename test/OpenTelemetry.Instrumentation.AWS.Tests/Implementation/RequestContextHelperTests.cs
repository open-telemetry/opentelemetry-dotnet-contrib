// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Runtime.Internal;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using OpenTelemetry.Trace;
using SNS = Amazon.SimpleNotificationService.Model;
using SQS = Amazon.SQS.Model;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class RequestContextHelperTests
{
    private const string TraceId = "5759e988bd862e3fe1be46a994272793";
    private const string ParentId = "53995c3f42cd8ad8";
    private const string OtherParentId = "a1b2c3d4e5f60718";
    private const string ApplicationParentId = "0102030405060708";
    private const string TraceState = "key1=value1,key2=value2";

    public RequestContextHelperTests()
    {
        Sdk.CreateTracerProviderBuilder()
            .Build();
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void AddAttributes_ParametersCollectionSizeReachesLimit_TraceDataNotInjected(string serviceType)
    {
        var originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 10);
        var parameters = new ParameterCollection();
        parameters.AddStringParameters(serviceType, originalRequest);

        var request = new TestRequest(parameters);

        var context = new TestRequestContext(originalRequest, request);

        var addAttributes = TestsHelper.CreateAddAttributesAction(serviceType);
        addAttributes?.Invoke(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        Assert.Equal(30, parameters.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SQS_AddAttributes_MessageAttributes_TraceDataInjected(bool initializeMessageAttributes)
    {
        var expectedParameters = new List<KeyValuePair<string, string>>
        {
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new("tracestate", "key1=value1,key2=value2"),
        };

        var originalRequest = new SQS.SendMessageRequest()
        {
            // The test parameter is used to simulate the AWS SDK's AWSConfigs.InitializeCollections setting.
            MessageAttributes = initializeMessageAttributes ? [] : null,
        };

        var context = new TestRequestContext(originalRequest, new TestRequest());

        SqsRequestContextHelper.AddAttributes(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertMessageParameters(expectedParameters, originalRequest);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SNS_AddAttributes_MessageAttributes_TraceDataInjected(bool initializeMessageAttributes)
    {
        var expectedParameters = new List<KeyValuePair<string, string>>
        {
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new("tracestate", "key1=value1,key2=value2"),
        };

        var originalRequest = new SNS.PublishRequest()
        {
            // The test parameter is used to simulate the AWS SDK's AWSConfigs.InitializeCollections setting.
            MessageAttributes = initializeMessageAttributes ? [] : null,
        };

        var context = new TestRequestContext(originalRequest, new TestRequest());

        SnsRequestContextHelper.AddAttributes(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertMessageParameters(expectedParameters, originalRequest);
    }

    [Fact]
    public void SQS_AddAttributes_MessageAttributesWithTraceParent_TraceStateNotInjected()
    {
        // This test just checks the common implementation logic:
        // if at least one attribute is already present the whole injection is skipped.
        // We just use default trace propagator as an example which injects only traceparent and tracestate.

        var traceParentValue = $"00-{TraceId}-{ParentId}-00";
        var expectedParameters = new List<KeyValuePair<string, string>>
        {
            new("traceparent", traceParentValue),
        };

        var originalRequest = new SQS.SendMessageRequest()
        {
            MessageAttributes = new Dictionary<string, SQS.MessageAttributeValue>
            {
                {
                    "traceparent",
                    new SQS.MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = traceParentValue,
                    }
                },
            },
        };

        var context = new TestRequestContext(originalRequest, new TestRequest());

        SqsRequestContextHelper.AddAttributes(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertMessageParameters(expectedParameters, originalRequest);
        Assert.DoesNotContain("tracestate", originalRequest.MessageAttributes);
    }

    [Fact]
    public void SNS_AddAttributes_MessageAttributesWithTraceParent_TraceStateNotInjected()
    {
        // This test just checks the common implementation logic:
        // if at least one attribute is already present the whole injection is skipped.
        // We just use default trace propagator as an example which injects only traceparent and tracestate.

        var traceParentValue = $"00-{TraceId}-{ParentId}-00";
        var expectedParameters = new List<KeyValuePair<string, string>>
        {
            new("traceparent", traceParentValue),
        };

        var originalRequest = new SNS.PublishRequest()
        {
            MessageAttributes = new Dictionary<string, SNS.MessageAttributeValue>
            {
                {
                    "traceparent",
                    new SNS.MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = traceParentValue,
                    }
                },
            },
        };

        var context = new TestRequestContext(originalRequest, new TestRequest());

        SnsRequestContextHelper.AddAttributes(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertMessageParameters(expectedParameters, originalRequest);
        Assert.DoesNotContain("tracestate", originalRequest.MessageAttributes);
    }

    [Fact]
    public void SQS_AddAttributes_SharedMessageAttributes_ApplicationDictionaryNotModified()
    {
        var shared = new Dictionary<string, SQS.MessageAttributeValue>
        {
            ["event-type"] = new() { DataType = "String", StringValue = "order-created" },
        };

        var first = new SQS.SendMessageRequest { MessageAttributes = shared };
        var second = new SQS.SendMessageRequest { MessageAttributes = shared };

        SqsRequestContextHelper.AddAttributes(new TestRequestContext(first, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(ParentId)));
        SqsRequestContextHelper.AddAttributes(new TestRequestContext(second, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(OtherParentId)));

        Assert.Single(shared);
        Assert.Equal($"00-{TraceId}-{ParentId}-00", first.MessageAttributes["traceparent"].StringValue);
        Assert.Equal($"00-{TraceId}-{OtherParentId}-00", second.MessageAttributes["traceparent"].StringValue);
        Assert.Equal("order-created", second.MessageAttributes["event-type"].StringValue);
    }

    [Fact]
    public void SQS_AddAttributes_RequestSentTwice_ContextReplaced()
    {
        var request = new SQS.SendMessageRequest
        {
            MessageAttributes = new Dictionary<string, SQS.MessageAttributeValue>
            {
                ["event-type"] = new() { DataType = "String", StringValue = "order-created" },
            },
        };

        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(ParentId)));
        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(OtherParentId)));

        Assert.Equal($"00-{TraceId}-{OtherParentId}-00", request.MessageAttributes["traceparent"].StringValue);
        Assert.Equal("order-created", request.MessageAttributes["event-type"].StringValue);
        Assert.Equal(3, request.MessageAttributes.Count);
    }

    [Fact]
    public void SNS_AddAttributes_SharedMessageAttributes_ApplicationDictionaryNotModified()
    {
        var shared = new Dictionary<string, SNS.MessageAttributeValue>
        {
            ["event-type"] = new() { DataType = "String", StringValue = "order-created" },
        };

        var first = new SNS.PublishRequest { MessageAttributes = shared };
        var second = new SNS.PublishRequest { MessageAttributes = shared };

        SnsRequestContextHelper.AddAttributes(new TestRequestContext(first, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(ParentId)));
        SnsRequestContextHelper.AddAttributes(new TestRequestContext(second, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(OtherParentId)));

        Assert.Single(shared);
        Assert.Equal($"00-{TraceId}-{ParentId}-00", first.MessageAttributes["traceparent"].StringValue);
        Assert.Equal($"00-{TraceId}-{OtherParentId}-00", second.MessageAttributes["traceparent"].StringValue);
        Assert.Equal("order-created", second.MessageAttributes["event-type"].StringValue);
    }

    [Fact]
    public void SNS_AddAttributes_RequestSentTwice_ContextReplaced()
    {
        var request = new SNS.PublishRequest
        {
            MessageAttributes = new Dictionary<string, SNS.MessageAttributeValue>
            {
                ["event-type"] = new() { DataType = "String", StringValue = "order-created" },
            },
        };

        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(ParentId)));
        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(OtherParentId)));

        Assert.Equal($"00-{TraceId}-{OtherParentId}-00", request.MessageAttributes["traceparent"].StringValue);
        Assert.Equal("order-created", request.MessageAttributes["event-type"].StringValue);
        Assert.Equal(3, request.MessageAttributes.Count);
    }

    [Fact]
    public void SQS_AddAttributes_CustomComparer_ComparerPreserved()
    {
        var request = new SQS.SendMessageRequest
        {
            MessageAttributes = new Dictionary<string, SQS.MessageAttributeValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["Event-Type"] = new() { DataType = "String", StringValue = "order-created" },
            },
        };

        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        Assert.Equal("order-created", request.MessageAttributes["event-type"].StringValue);
        Assert.Equal($"00-{TraceId}-{ParentId}-00", request.MessageAttributes["TRACEPARENT"].StringValue);
    }

    [Fact]
    public void SQS_AddAttributes_CustomComparer_ExistingTraceParentDifferentCase_NotInjected()
    {
        var request = new SQS.SendMessageRequest
        {
            MessageAttributes = new Dictionary<string, SQS.MessageAttributeValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["TraceParent"] = new() { DataType = "String", StringValue = "application-value" },
            },
        };

        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        Assert.Single(request.MessageAttributes);
        Assert.Equal("application-value", request.MessageAttributes["traceparent"].StringValue);
    }

    [Fact]
    public void SNS_AddAttributes_CustomComparer_ComparerPreserved()
    {
        var request = new SNS.PublishRequest
        {
            MessageAttributes = new Dictionary<string, SNS.MessageAttributeValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["Event-Type"] = new() { DataType = "String", StringValue = "order-created" },
            },
        };

        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        Assert.Equal("order-created", request.MessageAttributes["event-type"].StringValue);
        Assert.Equal($"00-{TraceId}-{ParentId}-00", request.MessageAttributes["TRACEPARENT"].StringValue);
    }

    [Fact]
    public void SNS_AddAttributes_CustomComparer_ExistingTraceParentDifferentCase_NotInjected()
    {
        var request = new SNS.PublishRequest
        {
            MessageAttributes = new Dictionary<string, SNS.MessageAttributeValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["TraceParent"] = new() { DataType = "String", StringValue = "application-value" },
            },
        };

        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        Assert.Single(request.MessageAttributes);
        Assert.Equal("application-value", request.MessageAttributes["traceparent"].StringValue);
    }

    [Fact]
    public void SQS_AddAttributes_RequestSentTwiceWithoutContext_InjectedAttributesRemoved()
    {
        var request = new SQS.SendMessageRequest
        {
            MessageAttributes = new Dictionary<string, SQS.MessageAttributeValue>
            {
                ["event-type"] = new() { DataType = "String", StringValue = "order-created" },
            },
        };

        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));
        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), new Dictionary<string, string>());

        Assert.Single(request.MessageAttributes);
        Assert.Equal("order-created", request.MessageAttributes["event-type"].StringValue);
    }

    [Fact]
    public void SQS_AddAttributes_NoContextAndNothingInjected_RequestNotModified()
    {
        var attributes = new Dictionary<string, SQS.MessageAttributeValue>
        {
            ["event-type"] = new() { DataType = "String", StringValue = "order-created" },
        };

        var request = new SQS.SendMessageRequest { MessageAttributes = attributes };

        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), new Dictionary<string, string>());

        Assert.Same(attributes, request.MessageAttributes);
    }

    [Fact]
    public void SQS_AddAttributes_InjectedValueChangedByApplication_ApplicationValuePreserved()
    {
        var request = new SQS.SendMessageRequest();

        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(ParentId)));

        var applicationValue = $"00-{TraceId}-{ApplicationParentId}-01";
        request.MessageAttributes["traceparent"].StringValue = applicationValue;

        SqsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(OtherParentId)));

        Assert.Equal(applicationValue, request.MessageAttributes["traceparent"].StringValue);
    }

    [Fact]
    public void SNS_AddAttributes_RequestSentTwiceWithoutContext_InjectedAttributesRemoved()
    {
        var request = new SNS.PublishRequest
        {
            MessageAttributes = new Dictionary<string, SNS.MessageAttributeValue>
            {
                ["event-type"] = new() { DataType = "String", StringValue = "order-created" },
            },
        };

        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));
        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), new Dictionary<string, string>());

        Assert.Single(request.MessageAttributes);
        Assert.Equal("order-created", request.MessageAttributes["event-type"].StringValue);
    }

    [Fact]
    public void SNS_AddAttributes_NoContextAndNothingInjected_RequestNotModified()
    {
        var attributes = new Dictionary<string, SNS.MessageAttributeValue>
        {
            ["event-type"] = new() { DataType = "String", StringValue = "order-created" },
        };

        var request = new SNS.PublishRequest { MessageAttributes = attributes };

        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), new Dictionary<string, string>());

        Assert.Same(attributes, request.MessageAttributes);
    }

    [Fact]
    public void SNS_AddAttributes_InjectedValueChangedByApplication_ApplicationValuePreserved()
    {
        var request = new SNS.PublishRequest();

        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(ParentId)));

        var applicationValue = $"00-{TraceId}-{ApplicationParentId}-01";
        request.MessageAttributes["traceparent"].StringValue = applicationValue;

        SnsRequestContextHelper.AddAttributes(new TestRequestContext(request, new TestRequest()), AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext(OtherParentId)));

        Assert.Equal(applicationValue, request.MessageAttributes["traceparent"].StringValue);
    }

    private static PropagationContext CreatePropagationContext(string parentSpanId = ParentId)
    {
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(parentSpanId.AsSpan());
        var traceFlags = ActivityTraceFlags.None;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags, TraceState, isRemote: true);

        return new PropagationContext(activityContext, Baggage.Current);
    }
}
