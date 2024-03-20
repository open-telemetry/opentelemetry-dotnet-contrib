// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Amazon.Runtime.Internal;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using OpenTelemetry.Trace;
using Xunit;
using SNS = Amazon.SimpleNotificationService.Model;
using SQS = Amazon.SQS.Model;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class RequestContextHelperTests
{
    private const string TraceId = "5759e988bd862e3fe1be46a994272793";
    private const string ParentId = "53995c3f42cd8ad8";
    private const string TraceState = "trace-state";

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

        var addAttributes = TestsHelper.CreateAddAttributesAction(serviceType, context);
        addAttributes?.Invoke(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        Assert.Equal(30, parameters.Count);
    }

    [Fact]
    public void SQS_AddAttributes_MessageAttributes_TraceDataInjected()
    {
        var expectedParameters = new List<KeyValuePair<string, string>>
        {
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new("tracestate", "trace-state"),
        };

        var originalRequest = new SQS.SendMessageRequest();

        var context = new TestRequestContext(originalRequest, new TestRequest());

        SqsRequestContextHelper.AddAttributes(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertMessageParameters(expectedParameters, originalRequest);
    }

    [Fact]
    public void SNS_AddAttributes_MessageAttributes_TraceDataInjected()
    {
        var expectedParameters = new List<KeyValuePair<string, string>>
        {
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new("tracestate", "trace-state"),
        };

        var originalRequest = new SNS.PublishRequest();

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

        var expectedParameters = new List<KeyValuePair<string, string>>
        {
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
        };

        var originalRequest = new SQS.SendMessageRequest();

        var context = new TestRequestContext(originalRequest, new TestRequest());

        SqsRequestContextHelper.AddAttributes(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertMessageParameters(expectedParameters, originalRequest);
    }

    [Fact]
    public void SNS_AddAttributes_MessageAttributesWithTraceParent_TraceStateNotInjected()
    {
        // This test just checks the common implementation logic:
        // if at least one attribute is already present the whole injection is skipped.
        // We just use default trace propagator as an example which injects only traceparent and tracestate.

        var expectedParameters = new List<KeyValuePair<string, string>>
        {
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
        };

        var originalRequest = new SNS.PublishRequest();

        var context = new TestRequestContext(originalRequest, new TestRequest());

        SnsRequestContextHelper.AddAttributes(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertMessageParameters(expectedParameters, originalRequest);
    }

    private static PropagationContext CreatePropagationContext()
    {
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
        var traceFlags = ActivityTraceFlags.None;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags, TraceState, isRemote: true);

        return new PropagationContext(activityContext, Baggage.Current);
    }
}
