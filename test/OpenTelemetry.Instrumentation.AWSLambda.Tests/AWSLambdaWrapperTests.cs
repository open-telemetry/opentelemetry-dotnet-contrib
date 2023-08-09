// <copyright file="AWSLambdaWrapperTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Moq;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests;

[Collection("TracerProviderDependent")]
public class AWSLambdaWrapperTests
{
    private const string TraceId = "5759e988bd862e3fe1be46a994272793";
    private const string XRayParentId = "53995c3f42cd8ad8";
    private const string CustomParentId = "11195c3f42cd8222";

    private readonly SampleHandlers sampleHandlers;
    private readonly SampleLambdaContext sampleLambdaContext;

    public AWSLambdaWrapperTests()
    {
        this.sampleHandlers = new SampleHandlers();
        this.sampleLambdaContext = new SampleLambdaContext();
        Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", $"Root=1-5759e988-bd862e3fe1be46a994272793;Parent={XRayParentId};Sampled=1");
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "testfunction");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION", "latest");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TraceSyncWithInputAndReturn(bool setCustomParent)
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddProcessor(processor.Object)
                   .Build()!)
        {
            var parentContext = setCustomParent ? CreateParentContext() : default;
            var result = AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncInputAndReturn, "TestStream", this.sampleLambdaContext, parentContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TraceSyncWithInputAndNoReturn(bool setCustomParent)
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddProcessor(processor.Object)
                   .Build()!)
        {
            var parentContext = setCustomParent ? CreateParentContext() : default;
            AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncInputAndNoReturn, "TestStream", this.sampleLambdaContext, parentContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TraceAsyncWithInputAndReturn(bool setCustomParent)
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddProcessor(processor.Object)
                   .Build()!)
        {
            var parentContext = setCustomParent ? CreateParentContext() : default;
            var result = await AWSLambdaWrapper.TraceAsync(tracerProvider, this.sampleHandlers.SampleHandlerAsyncInputAndReturn, "TestStream", this.sampleLambdaContext, parentContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TraceAsyncWithInputAndNoReturn(bool setCustomParent)
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddProcessor(processor.Object)
                   .Build()!)
        {
            var parentContext = setCustomParent ? CreateParentContext() : default;
            await AWSLambdaWrapper.TraceAsync(tracerProvider, this.sampleHandlers.SampleHandlerAsyncInputAndNoReturn, "TestStream", this.sampleLambdaContext, parentContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TestLambdaHandlerException(bool setCustomParent)
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddProcessor(processor.Object)
                   .Build()!)
        {
            try
            {
                var parentContext = setCustomParent ? CreateParentContext() : default;
                AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncNoReturnException, "TestException", this.sampleLambdaContext, parentContext);
            }
            catch
            {
                var resource = tracerProvider.GetResource();
                this.AssertResourceAttributes(resource);
            }
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
        this.AssertSpanException(activity);
    }

    [Fact]
    public void TestLambdaHandlerNotSampled()
    {
        Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=0");

        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddProcessor(processor.Object)
                   .Build()!)
        {
            var result = AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncInputAndReturn, "TestStream", this.sampleLambdaContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(4, processor.Invocations.Count);

        var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
        Assert.True(activities.Length == 0);
    }

    [Fact]
    public void OnFunctionStart_NoParent_ActivityCreated()
    {
        Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", null);

        Activity? activity = null;
        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .Build())
        {
            activity = AWSLambdaWrapper.OnFunctionStart("test-input", new Mock<ILambdaContext>().Object);
        }

        Assert.NotNull(activity);
    }

    [Fact]
    public void OnFunctionStart_NoSampledAndAwsXRayContextExtractionDisabled_ActivityCreated()
    {
        Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", $"Root=1-5759e988-bd862e3fe1be46a994272793;Parent={XRayParentId};Sampled=0");
        Activity? activity = null;

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations(c => c.DisableAwsXRayContextExtraction = true)
                   .Build())
        {
            activity = AWSLambdaWrapper.OnFunctionStart("test-input", new Mock<ILambdaContext>().Object);
        }

        Assert.NotNull(activity);
    }

    private static ActivityContext CreateParentContext()
    {
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(CustomParentId.AsSpan());
        return new ActivityContext(traceId, parentId, ActivityTraceFlags.Recorded);
    }

    private void AssertSpanProperties(Activity activity, string parentId)
    {
        Assert.Equal(TraceId, activity.TraceId.ToHexString());
        Assert.Equal(parentId, activity.ParentSpanId.ToHexString());
        Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        Assert.Equal(ActivityKind.Server, activity.Kind);
        Assert.Equal("testfunction", activity.DisplayName);
        Assert.Equal("OpenTelemetry.Instrumentation.AWSLambda", activity.Source.Name);

        // Version should consist of four decimals separated by dots.
        Assert.Matches(@"^\d+(\.\d+){3}$", activity.Source.Version);
    }

    private void AssertResourceAttributes(Resource? resource)
    {
        Assert.NotNull(resource);

        var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("aws", resourceAttributes[AWSLambdaSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("us-east-1", resourceAttributes[AWSLambdaSemanticConventions.AttributeCloudRegion]);
        Assert.Equal("testfunction", resourceAttributes[AWSLambdaSemanticConventions.AttributeFaasName]);
        Assert.Equal("latest", resourceAttributes[AWSLambdaSemanticConventions.AttributeFaasVersion]);
    }

    private void AssertSpanAttributes(Activity activity)
    {
        Assert.Equal(this.sampleLambdaContext.AwsRequestId, activity.GetTagValue(AWSLambdaSemanticConventions.AttributeFaasExecution));
        Assert.Equal(this.sampleLambdaContext.InvokedFunctionArn, activity.GetTagValue(AWSLambdaSemanticConventions.AttributeFaasID));
        Assert.Equal(this.sampleLambdaContext.FunctionName, activity.GetTagValue(AWSLambdaSemanticConventions.AttributeFaasName));
        Assert.Equal("other", activity.GetTagValue(AWSLambdaSemanticConventions.AttributeFaasTrigger));
        Assert.Equal("111111111111", activity.GetTagValue(AWSLambdaSemanticConventions.AttributeCloudAccountID));
    }

    private void AssertSpanException(Activity activity)
    {
        Assert.Equal("ERROR", activity.GetTagValue(SpanAttributeConstants.StatusCodeKey));
        Assert.NotNull(activity.GetTagValue(SpanAttributeConstants.StatusDescriptionKey));
    }
}
