// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using OpenTelemetry.Instrumentation.AWS.Implementation;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Implementation;

public class AWSTracingPipelineCustomizerTests
{
    [Theory]
    [InlineData(typeof(AmazonSQSClient), true)]
    [InlineData(typeof(AmazonSimpleNotificationServiceClient), true)]
    [InlineData(typeof(DerivedSqsClient), true)]
    [InlineData(typeof(DerivedSnsClient), true)]
    [InlineData(typeof(AmazonS3Client), false)]
    [InlineData(typeof(AmazonDynamoDBClient), false)]
    [InlineData(typeof(AmazonBedrockRuntimeClient), false)]
    [InlineData(typeof(AmazonServiceClient), false)]
    [InlineData(typeof(object), false)]
    public void IsMessagingClient_ReturnsExpectedValue(Type type, bool expected)
        => Assert.Equal(expected, AWSTracingPipelineCustomizer.IsMessagingClient(type));

    [Theory]
    [InlineData(typeof(AmazonSQSClient), SemanticConventionVersion.V1_45_0, 1)]
    [InlineData(typeof(AmazonSimpleNotificationServiceClient), SemanticConventionVersion.V1_45_0, 1)]
    [InlineData(typeof(AmazonSQSClient), SemanticConventionVersion.Latest, 1)]
    [InlineData(typeof(AmazonSQSClient), SemanticConventionVersion.V1_40_0, 0)]
    [InlineData(typeof(AmazonSQSClient), SemanticConventionVersion.V1_28_0, 0)]
    [InlineData(typeof(AmazonS3Client), SemanticConventionVersion.V1_45_0, 0)]
    [InlineData(typeof(AmazonDynamoDBClient), SemanticConventionVersion.Latest, 0)]
    [InlineData(typeof(AmazonBedrockRuntimeClient), SemanticConventionVersion.Latest, 0)]
    public void Customize_AddsMessageContextHandlerOnlyForMessagingClientsOfNewVersions(Type clientType, SemanticConventionVersion version, int expectedAdded)
    {
        var client = (AmazonServiceClient)Activator.CreateInstance(clientType, new AnonymousAWSCredentials(), RegionEndpoint.USEast1)!;

        try
        {
            var pipeline = (RuntimePipeline)typeof(AmazonServiceClient)
                .GetProperty("RuntimePipeline", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(client)!;

            var before = pipeline.Handlers.OfType<AWSMessageContextPipelineHandler>().Count();

            new AWSTracingPipelineCustomizer(new AWSClientInstrumentationOptions { SemanticConventionVersion = version })
                .Customize(clientType, pipeline);

            Assert.Equal(before + expectedAdded, pipeline.Handlers.OfType<AWSMessageContextPipelineHandler>().Count());
        }
        finally
        {
            client.Dispose();
        }
    }

    private sealed class DerivedSqsClient(AWSCredentials credentials, RegionEndpoint region) : AmazonSQSClient(credentials, region);

    private sealed class DerivedSnsClient(AWSCredentials credentials, RegionEndpoint region) : AmazonSimpleNotificationServiceClient(credentials, region);
}
