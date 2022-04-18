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
using Moq;
using OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.AWSLambda.Tests;

public class AWSLambdaWrapperTests
{
    private readonly SampleHandlers sampleHandlers;
    private readonly SampleLambdaContext sampleLambdaContext;

    public AWSLambdaWrapperTests()
    {
        this.sampleHandlers = new SampleHandlers();
        this.sampleLambdaContext = new SampleLambdaContext();
        Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1");
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "testfunction");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION", "latest");
    }

    [Fact]
    public void TestLambdaHandler()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            var result = AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncReturn, "TestStream", this.sampleLambdaContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity);
        this.AssertSpanAttributes(activity);
    }

    [Fact]
    public void TestLambdaHandlerNoReturn()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncNoReturn, "TestStream", this.sampleLambdaContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity);
        this.AssertSpanAttributes(activity);
    }

    [Fact]
    public async Task TestLambdaHandlerAsync()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            var result = await AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerAsyncReturn, "TestStream", this.sampleLambdaContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity);
        this.AssertSpanAttributes(activity);
    }

    [Fact]
    public async Task TestLambdaHandlerAsyncNoReturn()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            await AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerAsyncNoReturn, "TestStream", this.sampleLambdaContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity);
        this.AssertSpanAttributes(activity);
    }

    [Fact]
    public void TestLambdaHandlerNoContext()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            var result = AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncReturn, "TestStream");
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity);
    }

    [Fact]
    public void TestLambdaHandlerNoContextNoReturn()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncNoReturn, "TestStream");
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity);
    }

    [Fact]
    public async Task TestLambdaHandlerAsyncNoContext()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            var result = await AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerAsyncReturn, "TestStream");
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity);
    }

    [Fact]
    public async Task TestLambdaHandlerAsyncNoContextNoReturn()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            await AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerAsyncNoReturn, "TestStream");
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnStart -> OnEnd -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(6, processor.Invocations.Count);

        var activity = (Activity)processor.Invocations[1].Arguments[0];
        this.AssertSpanProperties(activity);
    }

    [Fact]
    public void TestLambdaHandlerException()
    {
        var processor = new Mock<BaseProcessor<Activity>>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSLambdaConfigurations()
            .AddProcessor(processor.Object)
            .Build())
        {
            try
            {
                AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncNoReturnException, "TestException", this.sampleLambdaContext);
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
        this.AssertSpanProperties(activity);
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
            .Build())
        {
            var result = AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncReturn, "TestStream", this.sampleLambdaContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        // SetParentProvider -> OnForceFlush -> OnShutdown -> Dispose
        Assert.Equal(4, processor.Invocations.Count);

        var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
        Assert.True(activities.Length == 0);
    }

    private void AssertSpanProperties(Activity activity)
    {
        Assert.Equal("5759e988bd862e3fe1be46a994272793", activity.TraceId.ToHexString());
        Assert.Equal("53995c3f42cd8ad8", activity.ParentSpanId.ToHexString());
        Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        Assert.Equal(ActivityKind.Server, activity.Kind);
        Assert.Equal("testfunction", activity.DisplayName);
    }

    private void AssertResourceAttributes(Resource resource)
    {
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
        Assert.Equal("111111111111", activity.GetTagValue(AWSLambdaSemanticConventions.AttributeCloudAccountID));
    }

    private void AssertSpanException(Activity activity)
    {
        Assert.Equal("ERROR", activity.GetTagValue(SpanAttributeConstants.StatusCodeKey));
        Assert.NotNull(activity.GetTagValue(SpanAttributeConstants.StatusDescriptionKey));
    }
}
