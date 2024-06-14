// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Resources.AWS;

internal static class AWSSemanticConventions
{
    public const string AttributeCloudAccountID = "cloud.account.id";
    public const string AttributeCloudAvailabilityZone = "cloud.availability_zone";
    public const string AttributeCloudPlatform = "cloud.platform";
    public const string AttributeCloudProvider = "cloud.provider";
    public const string AttributeCloudRegion = "cloud.region";
    public const string AttributeCloudResourceId = "cloud.resource_id";

    public const string AttributeContainerID = "container.id";

    public const string AttributeEcsContainerArn = "aws.ecs.container.arn";
    public const string AttributeEcsClusterArn = "aws.ecs.cluster.arn";
    public const string AttributeEcsLaunchtype = "aws.ecs.launchtype";
    public const string ValueEcsLaunchTypeEc2 = "ec2";
    public const string ValueEcsLaunchTypeFargate = "fargate";
    public const string AttributeEcsTaskArn = "aws.ecs.task.arn";
    public const string AttributeEcsTaskFamily = "aws.ecs.task.family";
    public const string AttributeEcsTaskRevision = "aws.ecs.task.revision";

    public const string AttributeFaasExecution = "faas.execution";
    public const string AttributeFaasID = "faas.id";
    public const string AttributeFaasName = "faas.name";
    public const string AttributeFaasVersion = "faas.version";

    public const string AttributeHostID = "host.id";
    public const string AttributeHostType = "host.type";
    public const string AttributeHostName = "host.name";

    public const string AttributeK8SClusterName = "k8s.cluster.name";

    public const string AttributeLogGroupNames = "aws.log.group.names";
    public const string AttributeLogGroupArns = "aws.log.group.arns";
    public const string AttributeLogStreamNames = "aws.log.stream.names";
    public const string AttributeLogStreamArns = "aws.log.stream.arns";

    public const string AttributeServiceName = "service.name";
    public const string AttributeServiceNamespace = "service.namespace";
    public const string AttributeServiceInstanceID = "service.instance.id";
    public const string AttributeServiceVersion = "service.version";
}
