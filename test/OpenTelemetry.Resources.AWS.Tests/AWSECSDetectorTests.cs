// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK

using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace OpenTelemetry.Resources.AWS.Tests;

public class AWSECSDetectorTests : IDisposable
{
    private const string AWSECSMetadataFilePath = "SampleMetadataFiles/testcgroup";
    private const string AWSECSMetadataURLKey = "ECS_CONTAINER_METADATA_URI";
    private const string AWSECSMetadataURLV4Key = "ECS_CONTAINER_METADATA_URI_V4";

    public AWSECSDetectorTests()
    {
        this.ResetEnvironment();
    }

    public void Dispose()
    {
        this.ResetEnvironment();
    }

    [Fact]
    public void TestNotOnEcs()
    {
        var ecsResourceDetector = new AWSECSDetector();
        var resourceAttributes = ecsResourceDetector.Detect();

        Assert.NotNull(resourceAttributes);
        Assert.Empty(resourceAttributes.Attributes);
    }

    [Fact]
    public void TestGetECSContainerId()
    {
        Assert.Equal("a4d00c9dd675d67f866c786181419e1b44832d4696780152e61afd44a3e02856", AWSECSDetector.GetECSContainerId(AWSECSMetadataFilePath));
    }

    [Fact]
    public void TestEcsMetadataV3()
    {
        Environment.SetEnvironmentVariable(AWSECSMetadataURLKey, "TestECSURIKey");

        var resourceAttributes = new AWSECSDetector().Detect().Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudProvider], "aws");
        Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudPlatform], "aws_ecs");
    }

    [Fact]
    public async Task TestEcsMetadataV4Ec2()
    {
        var source = new CancellationTokenSource();
        var token = source.Token;

        await using (var metadataEndpoint = new MockEcsMetadataEndpoint("ecs_metadata/metadatav4-response-container-ec2.json", "ecs_metadata/metadatav4-response-task-ec2.json"))
        {
            Environment.SetEnvironmentVariable(AWSECSMetadataURLV4Key, metadataEndpoint.Address.ToString());
            var resourceAttributes = new AWSECSDetector().Detect().Attributes.ToDictionary(x => x.Key, x => x.Value);

            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudProvider], "aws");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudPlatform], "aws_ecs");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudAccountID], "111122223333");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudAvailabilityZone], "us-west-2d");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudRegion], "us-west-2");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudResourceId], "arn:aws:ecs:us-west-2:111122223333:container/0206b271-b33f-47ab-86c6-a0ba208a70a9");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsContainerArn], "arn:aws:ecs:us-west-2:111122223333:container/0206b271-b33f-47ab-86c6-a0ba208a70a9");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsLaunchtype], "ec2");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsTaskArn], "arn:aws:ecs:us-west-2:111122223333:task/default/158d1c8083dd49d6b527399fd6414f5c");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsTaskFamily], "curltest");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsTaskRevision], "26");
#pragma warning disable CA1861 // Avoid constant arrays as arguments
            Assert.NotStrictEqual(resourceAttributes[AWSSemanticConventions.AttributeLogGroupNames], new string[] { "/ecs/metadata" });
            Assert.NotStrictEqual(resourceAttributes[AWSSemanticConventions.AttributeLogGroupArns], new string[] { "arn:aws:logs:us-west-2:111122223333:log-group:/ecs/metadata" });
            Assert.NotStrictEqual(resourceAttributes[AWSSemanticConventions.AttributeLogStreamNames], new string[] { "ecs/curl/8f03e41243824aea923aca126495f665" });
            Assert.NotStrictEqual(resourceAttributes[AWSSemanticConventions.AttributeLogStreamArns], new string[] { "arn:aws:logs:us-west-2:111122223333:log-group:/ecs/metadata:log-stream:ecs/curl/8f03e41243824aea923aca126495f665" });
#pragma warning restore CA1861 // Avoid constant arrays as arguments
        }
    }

    [Fact]
    public async Task TestEcsMetadataV4Fargate()
    {
        var source = new CancellationTokenSource();
        var token = source.Token;

        await using (var metadataEndpoint = new MockEcsMetadataEndpoint("ecs_metadata/metadatav4-response-container-fargate.json", "ecs_metadata/metadatav4-response-task-fargate.json"))
        {
            Environment.SetEnvironmentVariable(AWSECSMetadataURLV4Key, metadataEndpoint.Address.ToString());

            var resourceAttributes = new AWSECSDetector().Detect().Attributes.ToDictionary(x => x.Key, x => x.Value);

            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudProvider], "aws");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudPlatform], "aws_ecs");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudAccountID], "111122223333");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudAvailabilityZone], "us-west-2a");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudRegion], "us-west-2");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeCloudResourceId], "arn:aws:ecs:us-west-2:111122223333:container/05966557-f16c-49cb-9352-24b3a0dcd0e1");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsContainerArn], "arn:aws:ecs:us-west-2:111122223333:container/05966557-f16c-49cb-9352-24b3a0dcd0e1");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsLaunchtype], "fargate");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsTaskArn], "arn:aws:ecs:us-west-2:111122223333:task/default/e9028f8d5d8e4f258373e7b93ce9a3c3");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsTaskFamily], "curltest");
            Assert.Equal(resourceAttributes[AWSSemanticConventions.AttributeEcsTaskRevision], "3");
#pragma warning disable CA1861 // Avoid constant arrays as arguments
            Assert.NotStrictEqual(resourceAttributes[AWSSemanticConventions.AttributeLogGroupNames], new string[] { "/ecs/containerlogs" });
            Assert.NotStrictEqual(resourceAttributes[AWSSemanticConventions.AttributeLogGroupArns], new string[] { "arn:aws:logs:us-west-2:111122223333:log-group:/ecs/containerlogs" });
            Assert.NotStrictEqual(resourceAttributes[AWSSemanticConventions.AttributeLogStreamNames], new string[] { "ecs/curl/cd189a933e5849daa93386466019ab50" });
            Assert.NotStrictEqual(resourceAttributes[AWSSemanticConventions.AttributeLogStreamArns], new string[] { "arn:aws:logs:us-west-2:111122223333:log-group:/ecs/containerlogs:log-stream:ecs/curl/cd189a933e5849daa93386466019ab50" });
#pragma warning restore CA1861 // Avoid constant arrays as arguments
        }
    }

    internal void ResetEnvironment()
    {
        Environment.SetEnvironmentVariable(AWSECSMetadataURLKey, null);
        Environment.SetEnvironmentVariable(AWSECSMetadataURLV4Key, null);
    }

    internal class MockEcsMetadataEndpoint : IAsyncDisposable
    {
        public readonly Uri Address;
        private readonly IWebHost server;

        public MockEcsMetadataEndpoint(string containerJsonPath, string taskJsonPath)
        {
            this.server = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://127.0.0.1:0") // Use random localhost port
                .Configure(app =>
            {
                app.Run(async context =>
                {
                    if (context.Request.Method == HttpMethods.Get && context.Request.Path == "/")
                    {
                        var content = await File.ReadAllTextAsync($"{Environment.CurrentDirectory}/{containerJsonPath}");
                        var data = Encoding.UTF8.GetBytes(content);
                        context.Response.ContentType = "application/json";
                        await context.Response.Body.WriteAsync(data);
                    }
                    else if (context.Request.Method == HttpMethods.Get && context.Request.Path == "/task")
                    {
                        var content = await File.ReadAllTextAsync($"{Environment.CurrentDirectory}/{taskJsonPath}");
                        var data = Encoding.UTF8.GetBytes(content);
                        context.Response.ContentType = "application/json";
                        await context.Response.Body.WriteAsync(data);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsync("Not found");
                    }
                });
            }).Build();
            this.server.Start();

            this.Address = new Uri(this.server.ServerFeatures.Get<IServerAddressesFeature>()!.Addresses.First());
        }

        public async ValueTask DisposeAsync()
        {
            await this.DisposeAsyncCore();
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            await this.server.StopAsync();
        }
    }
}
#endif
