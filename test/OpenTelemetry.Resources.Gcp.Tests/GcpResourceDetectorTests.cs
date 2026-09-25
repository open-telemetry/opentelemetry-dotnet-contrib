// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Google.Api.Gax;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Resources.Gcp.Tests;

public class GcpResourceDetectorTests
{
    private const string ExecutionEnvVar = "CLOUD_RUN_EXECUTION";
    private const string TaskIndexEnvVar = "CLOUD_RUN_TASK_INDEX";

    [Fact]
    public void GcpResourceDetectorHandlesFailure()
    {
        var resource = ResourceBuilder.CreateEmpty()
            .AddGcpDetector()
            .Build();

        Assert.NotNull(resource);
        Assert.Null(resource.SchemaUrl);
    }

    [Fact]
    public void TestExtractGkeResourceAttributes()
    {
        var details = new GkePlatformDetails(
            metadataJson: "json",
            projectId: "projectId",
            clusterName: "clusterName",
            location: "location",
            hostName: "hostName",
            instanceId: "instanceId",
            zone: "us-central1-a",
            namespaceId: "namespaceId",
            podId: "podId",
            containerName: "containerName",
            clusterLocation: "clusterLocation");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGkeResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(8, attrs.Count);
        Assert.Equal(ResourceAttributeConstants.GcpCloudProviderValue, attrs[ResourceSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("projectId", attrs[ResourceSemanticConventions.AttributeCloudAccount]);
        Assert.Equal(ResourceAttributeConstants.GcpGkePlatformValue, attrs[ResourceSemanticConventions.AttributeCloudPlatform]);
        Assert.Equal("us-central1-a", attrs[ResourceSemanticConventions.AttributeCloudAvailabilityZone]);
        Assert.Equal("instanceId", attrs[ResourceSemanticConventions.AttributeHostId]);
        Assert.Equal("clusterName", attrs[ResourceSemanticConventions.AttributeK8sCluster]);
        Assert.Equal("namespaceId", attrs[ResourceSemanticConventions.AttributeK8sNamespace]);
        Assert.Equal("hostName", attrs[ResourceSemanticConventions.AttributeK8sPod]);
    }

    [Fact]
    public void TestExtractCloudRunResourceAttributes()
    {
        var details = new CloudRunPlatformDetails(
            metadataJson: "json",
            projectId: "projectId",
            zone: "us-central1-a",
            serviceName: "serviceName",
            revisionName: "revisionName",
            configurationName: "configurationName");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractCloudRunResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(7, attrs.Count);
        Assert.Equal(ResourceAttributeConstants.GcpCloudProviderValue, attrs[ResourceSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("projectId", attrs[ResourceSemanticConventions.AttributeCloudAccount]);
        Assert.Equal("us-central1-a", attrs[ResourceSemanticConventions.AttributeCloudAvailabilityZone]);
        Assert.Equal(ResourceAttributeConstants.GcpCloudRunPlatformValue, attrs[ResourceSemanticConventions.AttributeCloudPlatform]);
        Assert.Equal("us-central1", attrs[ResourceSemanticConventions.AttributeCloudRegion]);
        Assert.Equal("serviceName", attrs[ResourceSemanticConventions.AttributeFaasName]);
        Assert.Equal("revisionName", attrs[ResourceSemanticConventions.AttributeFaasVersion]);
    }

    [Fact]
    public void TestExtractCloudRunResourceAttributesWithInstanceId()
    {
        var cloudRunDetails = new CloudRunPlatformDetails(
            metadataJson: "json",
            projectId: "projectId",
            zone: "us-central1-a",
            serviceName: "serviceName",
            revisionName: "revisionName",
            configurationName: "configurationName");
        var platform = new Platform(cloudRunDetails);
        var gceDetails = new GcePlatformDetails(
            metadataJson: "json",
            projectId: "projectId",
            instanceId: "test-instance-id",
            zoneName: "us-central1-a");
        var attrs = CreateSampleCloudRunResourceAttributes(platform, gceDetails).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(8, attrs.Count);
        Assert.Equal("test-instance-id", attrs[ResourceSemanticConventions.AttributeFaasInstance]);
    }

    [Fact]
    public void TestExtractGaeResourceAttributes()
    {
        var details = new GaePlatformDetails(
            gcloudProject: "gcloudProject",
            gaeInstance: "gaeInstance",
            gaeService: "gaeService",
            gaeVersion: "gaeVersion");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGaeResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(3, attrs.Count);
        Assert.Equal(ResourceAttributeConstants.GcpCloudProviderValue, attrs[ResourceSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("gcloudProject", attrs[ResourceSemanticConventions.AttributeCloudAccount]);
        Assert.Equal(ResourceAttributeConstants.GcpGaePlatformValue, attrs[ResourceSemanticConventions.AttributeCloudPlatform]);
    }

    [Fact]
    public void TestExtractGceResourceAttributes()
    {
        var details = new GcePlatformDetails(
            metadataJson: "json",
            projectId: "projectId",
            instanceId: "instanceId",
            zoneName: "projects/12345/zones/us-central1-a");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGceResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(5, attrs.Count);
        Assert.Equal(ResourceAttributeConstants.GcpCloudProviderValue, attrs[ResourceSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("projectId", attrs[ResourceSemanticConventions.AttributeCloudAccount]);
        Assert.Equal(ResourceAttributeConstants.GcpGcePlatformValue, attrs[ResourceSemanticConventions.AttributeCloudPlatform]);
        Assert.Equal("instanceId", attrs[ResourceSemanticConventions.AttributeHostId]);
    }

    [Fact]
    public void TestExtractGceResourceAttributesWithMachineTypeAndImage()
    {
        var details = new GcePlatformDetails(
            metadataJson: """{"instance":{"machineType":"projects/12345/machineTypes/n1-standard-1","image":"projects/12345/global/images/imageName"}}""",
            projectId: "projectId",
            instanceId: "instanceId",
            zoneName: "projects/12345/zones/us-central1-a");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGceResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(7, attrs.Count);
        Assert.Equal("projects/12345/machineTypes/n1-standard-1", attrs[ResourceSemanticConventions.AttributeHostType]);
        Assert.Equal("imageName", attrs[ResourceSemanticConventions.AttributeHostImageName]);
    }

    [Fact]
    public void TestExtractGceResourceAttributesWithMachineTypeOnly()
    {
        var details = new GcePlatformDetails(
            metadataJson: """{"instance":{"machineType":"projects/12345/machineTypes/n1-standard-1"}}""",
            projectId: "projectId",
            instanceId: "instanceId",
            zoneName: "projects/12345/zones/us-central1-a");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGceResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(6, attrs.Count);
        Assert.Equal("projects/12345/machineTypes/n1-standard-1", attrs[ResourceSemanticConventions.AttributeHostType]);
        Assert.DoesNotContain(ResourceSemanticConventions.AttributeHostImageName, attrs.Keys);
    }

    [Fact]
    public void TestExtractGceResourceAttributesWithImageOnly()
    {
        var details = new GcePlatformDetails(
            metadataJson: """{"instance":{"image":"projects/12345/global/images/imageName"}}""",
            projectId: "projectId",
            instanceId: "instanceId",
            zoneName: "projects/12345/zones/us-central1-a");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGceResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(6, attrs.Count);
        Assert.Equal("imageName", attrs[ResourceSemanticConventions.AttributeHostImageName]);
        Assert.DoesNotContain(ResourceSemanticConventions.AttributeHostType, attrs.Keys);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"instance":"instance"}""")]
    [InlineData("""{"instance":{}}""")]
    [InlineData("""{"instance":{"machineType":null,"image":null}}""")]
    [InlineData("""{"instance":{"machineType":"","image":""}}""")]
    [InlineData("""{"instance":{"machineType":1,"image":1}}""")]
    public void TestExtractGceResourceAttributesWithoutMachineTypeAndImage(string metadataJson)
    {
        var details = new GcePlatformDetails(
            metadataJson: metadataJson,
            projectId: "projectId",
            instanceId: "instanceId",
            zoneName: "projects/12345/zones/us-central1-a");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGceResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(5, attrs.Count);
        Assert.DoesNotContain(ResourceSemanticConventions.AttributeHostType, attrs.Keys);
        Assert.DoesNotContain(ResourceSemanticConventions.AttributeHostImageName, attrs.Keys);
    }

    [Fact]
    public void TestExtractGceResourceAttributesLogsMalformedMetadata()
    {
        using var listener = new InMemoryEventListener(GcpResourcesEventSource.Log);

        var details = new GcePlatformDetails(
            metadataJson: "json",
            projectId: "projectId",
            instanceId: "instanceId",
            zoneName: "projects/12345/zones/us-central1-a");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGceResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(5, attrs.Count);

        var failed = Assert.Single(listener.Events, e => e.EventName == nameof(GcpResourcesEventSource.FailedToExtractResourceAttributes));

        Assert.Equal(EventLevel.Warning, failed.Level);
        Assert.Contains(nameof(GcpResourceDetector), failed.Payload!);
    }

    [Fact]
    public void DetectCloudRunJobPlatformDoesNotThrow()
    {
        var platform = new Platform(new CloudRunJobPlatformDetails("{}", "projectId", "us-central1-a", "jobName"));
        Assert.Null(platform.GceDetails);

        var resource = GcpResourceDetector.Detect(platform);

        Assert.NotNull(resource);
        Assert.NotNull(resource.SchemaUrl);

        var actual = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal(ResourceAttributeConstants.GcpCloudRunPlatformValue, actual[ResourceSemanticConventions.AttributeCloudPlatform]);
    }

    [Fact]
    public void TestExtractCloudRunJobResourceAttributes()
    {
        using var scope = EnvironmentVariableScope.Create(
            (ExecutionEnvVar, "jobName-abcde"),
            (TaskIndexEnvVar, "3"));

        var platform = new Platform(new CloudRunJobPlatformDetails("{}", "projectId", "us-central1-a", "jobName"));

        var actual = GcpResourceDetector.ExtractCloudRunJobResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(8, actual.Count);
        Assert.Equal(ResourceAttributeConstants.GcpCloudProviderValue, actual[ResourceSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("projectId", actual[ResourceSemanticConventions.AttributeCloudAccount]);
        Assert.Equal(ResourceAttributeConstants.GcpCloudRunPlatformValue, actual[ResourceSemanticConventions.AttributeCloudPlatform]);
        Assert.Equal("us-central1-a", actual[ResourceSemanticConventions.AttributeCloudAvailabilityZone]);
        Assert.Equal("us-central1", actual[ResourceSemanticConventions.AttributeCloudRegion]);
        Assert.Equal("jobName", actual[ResourceSemanticConventions.AttributeFaasName]);
        Assert.Equal("jobName-abcde", actual[ResourceAttributeConstants.AttributeGcpCloudRunJobExecution]);
        Assert.Equal(3, actual[ResourceAttributeConstants.AttributeGcpCloudRunJobTaskIndex]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    public void TestExtractCloudRunJobResourceAttributesWithoutJobEnvironment(string? taskIndex)
    {
        using var scope = EnvironmentVariableScope.Create(
            (ExecutionEnvVar, null),
            (TaskIndexEnvVar, taskIndex));

        var platform = new Platform(new CloudRunJobPlatformDetails("{}", "projectId", "us-central1-a", "jobName"));

        var actual = GcpResourceDetector.ExtractCloudRunJobResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(6, actual.Count);
        Assert.DoesNotContain(ResourceAttributeConstants.AttributeGcpCloudRunJobExecution, actual.Keys);
        Assert.DoesNotContain(ResourceAttributeConstants.AttributeGcpCloudRunJobTaskIndex, actual.Keys);
    }

    [Fact]
    public void TestExtractGceResourceAttributesWithoutGceDetails()
    {
        var platform = new Platform(new CloudRunJobPlatformDetails("{}", "projectId", "us-central1-a", "jobName"));
        Assert.Null(platform.GceDetails);

        var actual = GcpResourceDetector.ExtractGceResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(2, actual.Count);
        Assert.Equal(ResourceAttributeConstants.GcpCloudProviderValue, actual[ResourceSemanticConventions.AttributeCloudProvider]);
        Assert.Equal("projectId", actual[ResourceSemanticConventions.AttributeCloudAccount]);
        Assert.DoesNotContain(ResourceSemanticConventions.AttributeCloudPlatform, actual.Keys);
    }

    [Fact]
    public void DetectReturnsEmptyResourceForNullPlatform()
        => Assert.Equal(Resource.Empty, GcpResourceDetector.Detect(null));

    [Fact]
    public void TestExtractGceResourceAttributesDoesNotLogForValidMetadata()
    {
        using var listener = new InMemoryEventListener(GcpResourcesEventSource.Log);

        var details = new GcePlatformDetails(
            metadataJson: "{}",
            projectId: "projectId",
            instanceId: "instanceId",
            zoneName: "projects/12345/zones/us-central1-a");
        var platform = new Platform(details);
        var attrs = GcpResourceDetector.ExtractGceResourceAttributes(platform).ToDictionary(x => x.Key, x => x.Value);
        Assert.NotNull(attrs);
        Assert.Equal(5, attrs.Count);

        Assert.DoesNotContain(listener.Events, e => e.EventName == nameof(GcpResourcesEventSource.FailedToExtractResourceAttributes));
    }

    // Test method to extract Cloud Run resource attributes with sample GCE details
    private static List<KeyValuePair<string, object>> CreateSampleCloudRunResourceAttributes(Platform platform, GcePlatformDetails gceDetails)
    {
        var attributeList = new List<KeyValuePair<string, object>>
        {
            new(ResourceSemanticConventions.AttributeCloudProvider, ResourceAttributeConstants.GcpCloudProviderValue),
            new(ResourceSemanticConventions.AttributeCloudAccount, platform.ProjectId),
            new(ResourceSemanticConventions.AttributeCloudAvailabilityZone, platform.CloudRunDetails.Zone),
            new(ResourceSemanticConventions.AttributeCloudPlatform, ResourceAttributeConstants.GcpCloudRunPlatformValue),
            new(ResourceSemanticConventions.AttributeCloudRegion, platform.CloudRunDetails.Region),
            new(ResourceSemanticConventions.AttributeFaasName, platform.CloudRunDetails.ServiceName),
            new(ResourceSemanticConventions.AttributeFaasVersion, platform.CloudRunDetails.RevisionName),
        };

        // For faas.instance, use the GCE instance ID from the metadata service
        if (gceDetails != null && !string.IsNullOrEmpty(gceDetails.InstanceId))
        {
            attributeList.Add(new(ResourceSemanticConventions.AttributeFaasInstance, gceDetails.InstanceId));
        }

        return attributeList;
    }
}
