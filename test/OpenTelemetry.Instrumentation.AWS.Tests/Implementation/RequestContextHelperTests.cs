// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using OpenTelemetry.Trace;
using Xunit;

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
        AmazonWebServiceRequest originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 10);
        var parameters = new ParameterCollection();
        parameters.AddStringParameters(serviceType, originalRequest);

        var request = new TestRequest(parameters);

        var context = new TestRequestContext(originalRequest, request);

        var addAttributes = TestsHelper.CreateAddAttributesAction(serviceType, context);
        addAttributes?.Invoke(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        Assert.Equal(30, parameters.Count);
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void AddAttributes_ParametersCollection_TraceDataInjected(string serviceType)
    {
        var expectedParameters = new List<KeyValuePair<string, string>>()
        {
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new("tracestate", "trace-state"),
        };

        AmazonWebServiceRequest originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 0);
        var parameters = new ParameterCollection();

        var request = new TestRequest(parameters);
        var context = new TestRequestContext(originalRequest, request);

        var addAttributes = TestsHelper.CreateAddAttributesAction(serviceType, context);
        addAttributes?.Invoke(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertStringParameters(serviceType, expectedParameters, parameters);
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void AddAttributes_ParametersCollectionWithCustomParameter_TraceDataInjected(string serviceType)
    {
        var expectedParameters = new List<KeyValuePair<string, string>>()
        {
            new("name1", "value1"),
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
            new("tracestate", "trace-state"),
        };

        AmazonWebServiceRequest originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 1);
        var parameters = new ParameterCollection();
        parameters.AddStringParameters(serviceType, originalRequest);

        var request = new TestRequest(parameters);

        var context = new TestRequestContext(originalRequest, request);

        var addAttributes = TestsHelper.CreateAddAttributesAction(serviceType, context);
        addAttributes?.Invoke(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertStringParameters(serviceType, expectedParameters, parameters);
    }

    [Theory]
    [InlineData(AWSServiceType.SQSService)]
    [InlineData(AWSServiceType.SNSService)]
    public void AddAttributes_ParametersCollectionWithTraceParent_TraceStateNotInjected(string serviceType)
    {
        // This test just checks the common implementation logic:
        // if at least one attribute is already present the whole injection is skipped.
        // We just use default trace propagator as an example which injects only traceparent and tracestate.

        var expectedParameters = new List<KeyValuePair<string, string>>()
        {
            new("traceparent", $"00-{TraceId}-{ParentId}-00"),
        };

        AmazonWebServiceRequest originalRequest = TestsHelper.CreateOriginalRequest(serviceType, 0);
        originalRequest.AddAttribute("traceparent", $"00-{TraceId}-{ParentId}-00");

        var parameters = new ParameterCollection();
        parameters.AddStringParameters(serviceType, originalRequest);

        var request = new TestRequest(parameters);
        var context = new TestRequestContext(originalRequest, request);

        var addAttributes = TestsHelper.CreateAddAttributesAction(serviceType, context);
        addAttributes?.Invoke(context, AWSMessagingUtils.InjectIntoDictionary(CreatePropagationContext()));

        TestsHelper.AssertStringParameters(serviceType, expectedParameters, parameters);
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
