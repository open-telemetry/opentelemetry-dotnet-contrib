// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions;

namespace OpenTelemetry.AWS;

// disable Style Warnings to improve readability of this specific file.
#pragma warning disable SA1124
#pragma warning disable SA1005
#pragma warning disable SA1514
#pragma warning disable SA1201
#pragma warning disable SA1623

internal static partial class AWSSemanticConventions
{
    /// <summary>
    /// Defines all Semantic Conventions used by AWS extension projects.
    ///
    /// All values default to <c>string.Empty</c> and are then is only defined
    /// in the first version specific class (ie <see cref="AWSSemanticConventions_v1_10"/>)
    /// to use it.  This helps ensure the attribute doesn't get used if the user has specified
    /// a specific <see cref="SemanticConventionVersion"/>.
    ///
    /// See <see cref="GetSemanticConventionVersion"/> for details.
    /// </summary>
    private abstract class AWSSemanticConventionsBase
    {
        // CLOUD Attributes
        /// <inheritdoc cref="CloudAttributes.AttributeCloudAccountId"/>
        public virtual string AttributeCloudAccountID => string.Empty;
        /// <inheritdoc cref="CloudAttributes.AttributeCloudAvailabilityZone"/>
        public virtual string AttributeCloudAvailabilityZone => string.Empty;
        /// <inheritdoc cref="CloudAttributes.AttributeCloudPlatform"/>
        public virtual string AttributeCloudPlatform => string.Empty;
        /// <inheritdoc cref="CloudAttributes.AttributeCloudProvider"/>
        public virtual string AttributeCloudProvider => string.Empty;
        /// <inheritdoc cref="CloudAttributes.AttributeCloudRegion"/>
        public virtual string AttributeCloudRegion => string.Empty;
        /// <inheritdoc cref="CloudAttributes.AttributeCloudResourceId"/>
        public virtual string AttributeCloudResourceId => string.Empty;
        /// <inheritdoc cref="CloudAttributes.CloudPlatformValues.AwsEc2"/>
        public virtual string CloudPlatformValuesAwsEc2 => string.Empty;
        /// <inheritdoc cref="CloudAttributes.CloudPlatformValues.AwsEcs"/>
        public virtual string CloudPlatformValuesAwsEcs => string.Empty;
        /// <inheritdoc cref="CloudAttributes.CloudPlatformValues.AwsEks"/>
        public virtual string CloudPlatformValuesAwsEks => string.Empty;
        /// <inheritdoc cref="CloudAttributes.CloudPlatformValues.AwsElasticBeanstalk"/>
        public virtual string CloudPlatformValuesAwsElasticBeanstalk => string.Empty;
        /// <inheritdoc cref="CloudAttributes.CloudProviderValues.Aws"/>
        public virtual string CloudProviderValuesAws => string.Empty;

        // CONTAINER Attributes
        /// <inheritdoc cref="ContainerAttributes.AttributeContainerId"/>
        public virtual string AttributeContainerID => string.Empty;

        // DB Attributes
        /// <inheritdoc cref="DbAttributes.AttributeDbSystem"/>
        public virtual string AttributeDbSystem => string.Empty;
        /// <inheritdoc cref="DbAttributes.DbSystemValues.Dynamodb"/>
        public virtual string AttributeDynamoDb => string.Empty;

        // AWS Attributes
        /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsContainerArn"/>
        public virtual string AttributeEcsContainerArn => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsClusterArn"/>
        public virtual string AttributeEcsClusterArn => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsLaunchtype"/>
        public virtual string AttributeEcsLaunchtype => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AwsEcsLaunchtypeValues.Ec2"/>
        public virtual string ValueEcsLaunchTypeEc2 => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AwsEcsLaunchtypeValues.Fargate"/>
        public virtual string ValueEcsLaunchTypeFargate => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsTaskArn"/>
        public virtual string AttributeEcsTaskArn => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsTaskFamily"/>
        public virtual string AttributeEcsTaskFamily => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsTaskRevision"/>
        public virtual string AttributeEcsTaskRevision => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsLogGroupNames"/>
        public virtual string AttributeLogGroupNames => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsLogGroupArns"/>
        public virtual string AttributeLogGroupArns => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsLogStreamArns"/>
        public virtual string AttributeLogStreamNames => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsLogStreamNames"/>
        public virtual string AttributeLogStreamArns => string.Empty;
        /// <inheritdoc cref="AwsAttributes.AttributeAwsDynamodbTableNames"/>
        public virtual string AttributeAWSDynamoTableName => string.Empty;
        /// <summary>
        /// Not yet incorporated in Semantic Conventions repository.
        /// </summary>
        public virtual string AttributeAWSSQSQueueUrl => string.Empty;
        /// <summary>
        /// Not yet incorporated in Semantic Conventions repository.
        /// </summary>
        public virtual string AttributeAWSBedrockAgentId => string.Empty;
        /// <summary>
        /// Not yet incorporated in Semantic Conventions repository.
        /// </summary>
        public virtual string AttributeAWSBedrockDataSourceId => string.Empty;
        /// <summary>
        /// Not yet incorporated in Semantic Conventions repository.
        /// </summary>
        public virtual string AttributeAWSBedrockGuardrailId => string.Empty;
        /// <summary>
        /// Not yet incorporated in Semantic Conventions repository.
        /// </summary>
        public virtual string AttributeAWSBedrockKnowledgeBaseId => string.Empty;
        /// <summary>
        /// Not yet incorporated in Semantic Conventions repository.
        /// </summary>
        public virtual string AttributeAWSBedrock => string.Empty;

        // FAAS Attributes
        /// <inheritdoc cref="CloudAttributes.AttributeCloudResourceId"/>
        public virtual string AttributeFaasID => string.Empty;
        /// <inheritdoc cref="FaasAttributes.AttributeFaasInvocationId"/>
        public virtual string AttributeFaasExecution => string.Empty;
        /// <inheritdoc cref="FaasAttributes.AttributeFaasName"/>
        public virtual string AttributeFaasName => string.Empty;
        /// <inheritdoc cref="FaasAttributes.AttributeFaasVersion"/>
        public virtual string AttributeFaasVersion => string.Empty;
        /// <inheritdoc cref="FaasAttributes.AttributeFaasTrigger"/>
        public virtual string AttributeFaasTrigger => string.Empty;
        /// <inheritdoc cref="FaasAttributes.AttributeFaasColdstart"/>
        public virtual string AttributeFaasColdStart => string.Empty;

        // GEN AI Attributes
        /// <inheritdoc cref="GenAiAttributes.AttributeGenAiRequestModel"/>
        public virtual string AttributeGenAiModelId => string.Empty;
        /// <inheritdoc cref="GenAiAttributes.AttributeGenAiSystem"/>
        public virtual string AttributeGenAiSystem => string.Empty;

        // HOST Attributes
        /// <inheritdoc cref="HostAttributes.AttributeHostId"/>
        public virtual string AttributeHostID => string.Empty;
        /// <inheritdoc cref="HostAttributes.AttributeHostType"/>
        public virtual string AttributeHostType => string.Empty;
        /// <inheritdoc cref="HostAttributes.AttributeHostName"/>
        public virtual string AttributeHostName => string.Empty;

        // HTTP Attributes
        /// <inheritdoc cref="HttpAttributes.AttributeHttpStatusCode"/>
        public virtual string AttributeHttpStatusCode => string.Empty;
        /// <inheritdoc cref="HttpAttributes.AttributeHttpResponseStatusCode"/>
        public virtual string AttributeHttpResponseStatusCode => string.Empty;
        /// <inheritdoc cref="HttpAttributes.AttributeHttpScheme"/>
        public virtual string AttributeHttpScheme => string.Empty;
        /// <inheritdoc cref="HttpAttributes.AttributeHttpTarget"/>
        public virtual string AttributeHttpTarget => string.Empty;
        /// <inheritdoc cref="HttpAttributes.AttributeHttpMethod"/>
        public virtual string AttributeHttpMethod => string.Empty;
        /// <inheritdoc cref="HttpAttributes.AttributeHttpRequestMethod"/>
        public virtual string AttributeHttpRequestMethod => string.Empty;

        // NET Attributes
        /// <inheritdoc cref="NetAttributes.AttributeNetHostName"/>
        public virtual string AttributeNetHostName => string.Empty;
        /// <inheritdoc cref="NetAttributes.AttributeNetHostPort"/>
        public virtual string AttributeNetHostPort => string.Empty;

        // SERVER Attributes
        /// <inheritdoc cref="ServerAttributes.AttributeServerAddress"/>
        public virtual string AttributeServerAddress => string.Empty;
        /// <inheritdoc cref="ServerAttributes.AttributeServerPort"/>
        public virtual string AttributeServerPort => string.Empty;

        // K8s Attributes
        /// <inheritdoc cref="K8sAttributes.AttributeK8sClusterName"/>
        public virtual string AttributeK8SClusterName => string.Empty;

        // SERVICE Attributes
        /// <inheritdoc cref="ServiceAttributes.AttributeServiceName"/>
        public virtual string AttributeServiceName => string.Empty;
        /// <inheritdoc cref="ServiceAttributes.AttributeServiceNamespace"/>
        public virtual string AttributeServiceNamespace => string.Empty;
        /// <inheritdoc cref="ServiceAttributes.AttributeServiceInstanceId"/>
        public virtual string AttributeServiceInstanceID => string.Empty;
        /// <inheritdoc cref="ServiceAttributes.AttributeServiceVersion"/>
        public virtual string AttributeServiceVersion => string.Empty;
        /// <summary>
        /// Not yet incorporated in Semantic Conventions repository.
        /// </summary>
        public virtual string ServiceNameValuesAwsElasticBeanstalk => string.Empty;

        // URL Attributes
        /// <inheritdoc cref="UrlAttributes.AttributeUrlPath"/>
        public virtual string AttributeUrlPath => string.Empty;
        /// <inheritdoc cref="UrlAttributes.AttributeUrlQuery"/>
        public virtual string AttributeUrlQuery => string.Empty;
        /// <inheritdoc cref="UrlAttributes.AttributeUrlScheme"/>
        public virtual string AttributeUrlScheme => string.Empty;
    }
}
