// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Resources;
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
        var exportedItems = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddInMemoryExporter(exportedItems)
                   .Build()!)
        {
            var parentContext = setCustomParent ? CreateParentContext() : default;
            var result = AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncInputAndReturn, "TestStream", this.sampleLambdaContext, parentContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        Assert.Single(exportedItems);

        var activity = exportedItems[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TraceSyncWithInputAndNoReturn(bool setCustomParent)
    {
        var exportedItems = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddInMemoryExporter(exportedItems)
                   .Build()!)
        {
            var parentContext = setCustomParent ? CreateParentContext() : default;
            AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncInputAndNoReturn, "TestStream", this.sampleLambdaContext, parentContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        Assert.Single(exportedItems);

        var activity = exportedItems[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TraceAsyncWithInputAndReturn(bool setCustomParent)
    {
        var exportedItems = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddInMemoryExporter(exportedItems)
                   .Build()!)
        {
            var parentContext = setCustomParent ? CreateParentContext() : default;
            var result = await AWSLambdaWrapper.TraceAsync(tracerProvider, this.sampleHandlers.SampleHandlerAsyncInputAndReturn, "TestStream", this.sampleLambdaContext, parentContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        Assert.Single(exportedItems);

        var activity = exportedItems[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TraceAsyncWithInputAndNoReturn(bool setCustomParent)
    {
        var exportedItems = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddInMemoryExporter(exportedItems)
                   .Build()!)
        {
            var parentContext = setCustomParent ? CreateParentContext() : default;
            await AWSLambdaWrapper.TraceAsync(tracerProvider, this.sampleHandlers.SampleHandlerAsyncInputAndNoReturn, "TestStream", this.sampleLambdaContext, parentContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        Assert.Single(exportedItems);

        var activity = exportedItems[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TestLambdaHandlerException(bool setCustomParent)
    {
        var exportedItems = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddInMemoryExporter(exportedItems)
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

        Assert.Single(exportedItems);

        var activity = exportedItems[0];
        this.AssertSpanProperties(activity, setCustomParent ? CustomParentId : XRayParentId);
        this.AssertSpanAttributes(activity);
        this.AssertSpanException(activity);
    }

    [Fact]
    public void TestLambdaHandlerNotSampled()
    {
        Environment.SetEnvironmentVariable("_X_AMZN_TRACE_ID", "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=0");

        var exportedItems = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                   .AddAWSLambdaConfigurations()
                   .AddInMemoryExporter(exportedItems)
                   .Build()!)
        {
            var result = AWSLambdaWrapper.Trace(tracerProvider, this.sampleHandlers.SampleHandlerSyncInputAndReturn, "TestStream", this.sampleLambdaContext);
            var resource = tracerProvider.GetResource();
            this.AssertResourceAttributes(resource);
        }

        Assert.Empty(exportedItems);
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
            activity = AWSLambdaWrapper.OnFunctionStart("test-input", new SampleLambdaContext());
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
            activity = AWSLambdaWrapper.OnFunctionStart("test-input", new SampleLambdaContext());
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

        // Version should consist of 3 decimals separated by dots followed by optional pre-release suffix
        Assert.Matches(@"^\d+(\.\d+){2}(-.+)?$", activity.Source.Version);
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
