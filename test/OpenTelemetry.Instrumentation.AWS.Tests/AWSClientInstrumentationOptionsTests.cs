// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using OpenTelemetry.Instrumentation.AWS.Implementation;
using OpenTelemetry.Instrumentation.AWS.Tests.Tools;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWS.Tests;

/// <summary>
/// Tests for AWS Semantic Conversion via
/// <see cref="AWSClientInstrumentationOptions.SemanticConventionVersion"/>.
/// These tests verify that the switching mechanism works rather than
/// explicitly verifying what the semantic convention should be.
/// </summary>
public sealed class AWSClientInstrumentationOptionsTests
{
    [Fact]
    public void CanUseSemanticConvention_V1_28_0()
    {
        var semanticVersion = SemanticConventionVersion.V1_28_0;

        var tags = this.GetActivityTags(semanticVersion);

        // GenAI System attribute differs slightly between 1.28 and 1.29
        Assert.Equal("aws_bedrock", tags["gen_ai.system"]);
    }

    [Fact]
    public void CanUseSemanticConvention_V1_29_0()
    {
        var semanticVersion = SemanticConventionVersion.V1_29_0;

        var tags = this.GetActivityTags(semanticVersion);

        // GenAI System attribute differs slightly between 1.28 and 1.29
        Assert.Equal("aws.bedrock", tags["gen_ai.system"]);
    }

    private Dictionary<string, string?> GetActivityTags(SemanticConventionVersion semVersion)
    {
        var exportedItems = new List<Activity>();

        var parent = new Activity("parent").Start();
        var requestId = @"fakerequ-esti-dfak-ereq-uestidfakere";

        try
        {
            using (Sdk.CreateTracerProviderBuilder()
                       .AddXRayTraceId()
                       .SetSampler(new AlwaysOnSampler())
                       .AddAWSInstrumentation(o =>
                       {
                           o.SemanticConventionVersion = semVersion;
                       })
                       .AddInMemoryExporter(exportedItems)
                       .Build())
            {
                var bedrockruntime = new AmazonBedrockRuntimeClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);
                var dummyResponse = "{}";
                CustomResponses.SetResponse(bedrockruntime, dummyResponse, requestId, true);
                var invokeModelRequest = new InvokeModelRequest { ModelId = "amazon.titan-text-express-v1" };

#if NETFRAMEWORK
                var response = bedrockruntime.InvokeModel(invokeModelRequest);
#else
                var response = bedrockruntime.InvokeModelAsync(invokeModelRequest).Result;
#endif
            }
        }
        finally
        {
            // unregister the AWS Runtime Pipeline Customizer, or it will conflict
            // with the next test run
            RuntimePipelineCustomizerRegistry.Instance.Deregister(AWSTracingPipelineCustomizer.UniqueName);
        }

        return
            exportedItems
                .FirstOrDefault(a => a.DisplayName == "Bedrock Runtime.InvokeModel")
                ?.Tags
                .ToDictionary(x => x.Key, x => x.Value)
                ?? new Dictionary<string, string?>();
    }
}
