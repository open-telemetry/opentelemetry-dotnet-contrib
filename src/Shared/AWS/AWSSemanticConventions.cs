// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable SA1124
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTelemetry.SemanticConventions;

using Ver = OpenTelemetry.AWS.SemanticConventionVersion;

namespace OpenTelemetry.AWS;

/// <summary>
/// TODO
/// https://opentelemetry.io/docs/specs/otel/versioning-and-stability/
/// </summary>
public enum SemanticConventionVersion
{
    /// <summary>
    /// Pin to the specific state of all Semantic Conventions as of the 0.10 release
    /// </summary>
    v0_10_EXPERIMENTAL,

    /// <summary>
    /// Pin to the specific state of all Semantic Conventions as of the 0.11 release
    /// </summary>
    v0_11_EXPERIMENTAL,

    /// <summary>
    /// Use Experimental Conventions until they become stable and then pin to stable.
    /// </summary>
    EXPERIMENTAL_UNTIL_STABLE,

    /// <summary>
    /// Always uwe the latest version of a Semantic Convention even if there is a stable version.
    /// </summary>
    ALWAYS_EXPERIMENTAL
};

/// <summary>
/// TODO - update documentation
/// Defines extension methods for adding attributes to a collection.
/// <para />
/// This approach supports a forward looking with regard to experimental Attributes.  If an
/// Attribute is changed, update the extension method so that it emits -both- the old and new
/// attribute names.
/// <para />
/// On the next major version bump, cease emitting out-of-date Attributes.
/// </summary>
internal static class AWSSemanticConventions
{
    public static SemanticConventionVersion SemanticConventionVersion { get; set; }

    /// <summary>
    /// TODO
    /// </summary>
    internal const SemanticConventionVersion DefaultSemanticConventionVersion = SemanticConventionVersion.EXPERIMENTAL_UNTIL_STABLE;

    // Cloud Attributes
    private static readonly Func<Ver, string> AttributeCloudAccountID = (_) => "cloud.account.id"; //CloudAttributes.AttributeCloudAccountId
    private static readonly Func<Ver, string> AttributeCloudAvailabilityZone = (_) => "cloud.availability_zone"; //CloudAttributes.AttributeCloudAvailabilityZone;
    private static readonly Func<Ver, string> AttributeCloudPlatform = (_) => "cloud.platform"; //CloudAttributes.AttributeCloudPlatform;
    private static readonly Func<Ver, string> AttributeCloudProvider = (_) => "cloud.provider"; //CloudAttributes.AttributeCloudProvider;
    private static readonly Func<Ver, string> AttributeCloudRegion = (_) => "cloud.region"; //CloudAttributes.AttributeCloudRegion;
    private static readonly Func<Ver, string> AttributeCloudResourceId = (_) => "cloud.resource_id"; //CloudAttributes.AttributeCloudResourceId;
    public const string CloudPlatformValuesAwsEc2 = CloudAttributes.CloudPlatformValues.AwsEc2;
    public const string CloudPlatformValuesAwsEcs = CloudAttributes.CloudPlatformValues.AwsEcs;
    public const string CloudPlatformValuesAwsEks = CloudAttributes.CloudPlatformValues.AwsEks;
    public const string CloudPlatformValuesAwsElasticBeanstalk = CloudAttributes.CloudPlatformValues.AwsElasticBeanstalk;
    public const string CloudProviderValuesAws = CloudAttributes.CloudProviderValues.Aws;

    // Container Attributes
    private static readonly Func<Ver, string> AttributeContainerID = (_) => ContainerAttributes.AttributeContainerId;

    // Db Attributes
    private static readonly Func<Ver, string> AttributeDbSystem = (_) => DbAttributes.AttributeDbSystem;

    // AWS Attributes
    private static readonly Func<Ver, string> AttributeEcsContainerArn = (_) => AwsAttributes.AttributeAwsEcsContainerArn;
    private static readonly Func<Ver, string> AttributeEcsClusterArn = (_) => AwsAttributes.AttributeAwsEcsClusterArn;
    private static readonly Func<Ver, string> AttributeEcsLaunchtype = (_) => AwsAttributes.AttributeAwsEcsLaunchtype;
    private static readonly Func<Ver, string> ValueEcsLaunchTypeEc2 = (_) => AwsAttributes.AwsEcsLaunchtypeValues.Ec2;
    private static readonly Func<Ver, string> ValueEcsLaunchTypeFargate = (_) => AwsAttributes.AwsEcsLaunchtypeValues.Fargate;
    private static readonly Func<Ver, string> AttributeEcsTaskArn = (_) => AwsAttributes.AttributeAwsEcsTaskArn;
    private static readonly Func<Ver, string> AttributeEcsTaskFamily = (_) => AwsAttributes.AttributeAwsEcsTaskFamily;
    private static readonly Func<Ver, string> AttributeEcsTaskRevision = (_) => AwsAttributes.AttributeAwsEcsTaskRevision;
    private static readonly Func<Ver, string> AttributeLogGroupNames = (_) => AwsAttributes.AttributeAwsLogGroupNames;
    private static readonly Func<Ver, string> AttributeLogGroupArns = (_) => AwsAttributes.AttributeAwsLogGroupArns;
    private static readonly Func<Ver, string> AttributeLogStreamNames = (_) => AwsAttributes.AttributeAwsLogStreamArns;
    private static readonly Func<Ver, string> AttributeLogStreamArns = (_) => AwsAttributes.AttributeAwsLogStreamNames;
    private static readonly Func<Ver, string> AttributeAWSDynamoTableName = (_) => AwsAttributes.AttributeAwsDynamodbTableNames;
    private static readonly Func<Ver, string> AttributeAWSSQSQueueUrl = (_) => "aws.queue_url"; // todo - confirm in java;

    private static readonly Func<Ver, string> AttributeAWSBedrockAgentId = (_) => "aws.bedrock.agent.id";
    private static readonly Func<Ver, string> AttributeAWSBedrockDataSourceId = (_) => "aws.bedrock.data_source.id";
    private static readonly Func<Ver, string> AttributeAWSBedrockGuardrailId = (_) => "aws.bedrock.guardrail.id";
    private static readonly Func<Ver, string> AttributeAWSBedrockKnowledgeBaseId = (_) => "aws.bedrock.knowledge_base.id";

    private static readonly Func<Ver, string> AttributeAWSBedrock = (v) => v switch
    {
        SemanticConventionVersion.v0_10_EXPERIMENTAL => "aws_bedrock",
        _ => "aws.bedrock",
    };

    // Faas Attributes
    private static readonly Func<Ver, string> AttributeFaasID = (_) => CloudAttributes.AttributeCloudResourceId;
    private static readonly Func<Ver, string> AttributeFaasExecution = (_) => FaasAttributes.AttributeFaasInvocationId;
    private static readonly Func<Ver, string> AttributeFaasName = (_) => FaasAttributes.AttributeFaasName;
    private static readonly Func<Ver, string> AttributeFaasVersion = (_) => FaasAttributes.AttributeFaasVersion;
    private static readonly Func<Ver, string> AttributeFaasTrigger = (_) => FaasAttributes.AttributeFaasTrigger;
    private static readonly Func<Ver, string> AttributeFaasColdStart = (_) => FaasAttributes.AttributeFaasColdstart;

    // Gen AI Attributes
    private static readonly Func<Ver, string> AttributeGenAiModelId = (_) => GenAiAttributes.AttributeGenAiRequestModel;
    private static readonly Func<Ver, string> AttributeGenAiSystem = (_) => GenAiAttributes.AttributeGenAiSystem;

    // Host Attributes
    private static readonly Func<Ver, string> AttributeHostID = (_) => HostAttributes.AttributeHostId;
    private static readonly Func<Ver, string> AttributeHostType = (_) => HostAttributes.AttributeHostType;
    private static readonly Func<Ver, string> AttributeHostName = (_) => HostAttributes.AttributeHostName;

    // Http Attributes
    private static readonly Func<Ver, string> AttributeHttpStatusCode = (_) => HttpAttributes.AttributeHttpStatusCode;
    private static readonly Func<Ver, string> AttributeHttpScheme = (_) => HttpAttributes.AttributeHttpScheme;
    private static readonly Func<Ver, string> AttributeHttpTarget = (_) => HttpAttributes.AttributeHttpTarget;
    private static readonly Func<Ver, string> AttributeHttpMethod = (_) => HttpAttributes.AttributeHttpMethod;

    // Net Attributes
    private static readonly Func<Ver, string> AttributeNetHostName = (_) => NetAttributes.AttributeNetHostName;
    private static readonly Func<Ver, string> AttributeNetHostPort = (_) => NetAttributes.AttributeNetHostPort;

    // K8s Attributes
    private static readonly Func<Ver, string> AttributeK8SClusterName = (_) => K8sAttributes.AttributeK8sClusterName;

    // Service Attributes
    private static readonly Func<Ver, string> AttributeServiceName = (_) => ServiceAttributes.AttributeServiceName;
    private static readonly Func<Ver, string> AttributeServiceNamespace = (_) => ServiceAttributes.AttributeServiceNamespace;
    private static readonly Func<Ver, string> AttributeServiceInstanceID = (_) => ServiceAttributes.AttributeServiceInstanceId;
    private static readonly Func<Ver, string> AttributeServiceVersion = (_) => ServiceAttributes.AttributeServiceVersion;
    public static string ServiceNameValuesAwsElasticBeanstalk = "aws_elastic_beanstalk";

    #region Service Parameter Mapping

    public static IDictionary<string, string> AddAttributeAWSDynamoTableName(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSDynamoTableName, value);

    public static IDictionary<string, string> AddAttributeAWSSQSQueueUrl(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSSQSQueueUrl, value);

    public static IDictionary<string, string> AddAttributeGenAiModelId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeGenAiModelId, value);

    public static IDictionary<string, string> AddAttributeAWSBedrockAgentId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSBedrockAgentId, value);

    public static IDictionary<string, string> AddAttributeAWSBedrockDataSourceId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSBedrockDataSourceId, value);

    public static IDictionary<string, string> AddAttributeAWSBedrockGuardrailId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSBedrockGuardrailId, value);
    public static IDictionary<string, string> AddAttributeAWSBedrockKnowledgeBaseId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSBedrockKnowledgeBaseId, value);
    #endregion

    #region Cloud Attributes
    public static T AddAttributeCloudAccountID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudAccountID, value, addIfNull);

    public static T AddAttributeCloudAvailabilityZone<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudAvailabilityZone, value, addIfNull);

    public static T AddAttributeCloudPlatform<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudPlatform, value, addIfNull);

    public static T AddAttributeCloudProvider<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudProvider, value, addIfNull);

    public static T AddAttributeCloudRegion<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudRegion, value, addIfNull);

    public static T AddAttributeCloudResourceId<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudResourceId, value, addIfNull);
    #endregion

    #region Container
    public static T AddAttributeContainerId<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeContainerID, value, addIfNull);
    #endregion

    #region AWS
    public static Activity? SetTagAttributeDbSystemToDynamoDb(this Activity? activity)
        => SetTag(activity, AttributeDbSystem, DbAttributes.DbSystemValues.Dynamodb); // <---- todo

    public static Activity? SetTagAttributeGenAiSystemToBedrock(this Activity? activity)
        => SetTag(activity, AttributeGenAiSystem, AttributeAWSBedrock(SemanticConventionVersion));

    public static T AddAttributeEcsContainerArn<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsContainerArn, value, addIfNull);

    public static T AddAttributeEcsClusterArn<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsClusterArn, value, addIfNull);

    public static T AddAttributeEcsLaunchtype<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsLaunchtype, value, addIfNull);

    public static T AddAttributeEcsLaunchtypeIsEc2<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => AddAttributeEcsLaunchtype(attributes, AWSSemanticConventions.ValueEcsLaunchTypeEc2);

    public static T AddAttributeEcsLaunchtypeIsFargate<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => AddAttributeEcsLaunchtype(attributes, AWSSemanticConventions.ValueEcsLaunchTypeFargate);

    public static T AddAttributeEcsTaskArn<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsTaskArn, value, addIfNull);

    public static T AddAttributeEcsTaskFamily<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsTaskFamily, value, addIfNull);

    public static T AddAttributeEcsTaskRevision<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsTaskRevision, value, addIfNull);

    public static T AddAttributeLogGroupNames<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeLogGroupNames, value, addIfNull);

    public static T AddAttributeLogGroupArns<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeLogGroupArns, value, addIfNull);

    public static T AddAttributeLogStreamNames<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeLogStreamNames, value, addIfNull);

    public static T AddAttributeLogStreamArns<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeLogStreamArns, value, addIfNull);
    #endregion

    #region Faas
    public static T AddAttributeFaasID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasID, value, addIfNull);

    public static T AddAttributeFaasExecution<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasExecution, value, addIfNull);

    public static T AddAttributeFaasName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasName, value, addIfNull);

    public static T AddAttributeFaasVersion<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasVersion, value, addIfNull);

    public static T AddAttributeFaasTrigger<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasTrigger, value, addIfNull);

    public static T AddAttributeFaasColdStart<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasColdStart, value, addIfNull);

    #endregion

    #region Host
    public static T AddAttributeHostID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHostID, value, addIfNull);

    public static T AddAttributeHostType<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHostType, value, addIfNull);

    public static T AddAttributeHostName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHostName, value, addIfNull);
    #endregion

    #region Http

    public static Activity? SetTagAttributeHttpStatusCode(this Activity? activity, int value)
        => SetTag(activity, AttributeHttpStatusCode, value);

    public static T AddAttributeHttpScheme<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHttpScheme, value, addIfNull);

    public static T AddAttributeHttpTarget<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHttpTarget, value, addIfNull);

    public static T AddAttributeHttpMethod<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHttpMethod, value, addIfNull);
    #endregion

    #region Net
    public static T AddAttributeNetHostName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeNetHostName, value, addIfNull);

    public static T AddAttributeNetHostPort<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeNetHostPort, value, addIfNull);
    #endregion

    #region K8s
    public static T AddAttributeK8SClusterName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeK8SClusterName, value, addIfNull);

    public static T AddAttributeContainerID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeContainerID, value, addIfNull);
    #endregion

    #region Service
    public static T AddAttributeServiceName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeServiceName, value, addIfNull);

    public static T AddAttributeServiceNamespace<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeServiceNamespace, value, addIfNull);

    public static T AddAttributeServiceInstanceID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeServiceInstanceID, value, addIfNull);

    public static T AddAttributeServiceVersion<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeServiceVersion, value, addIfNull);
    #endregion

    private static T Add<T>(this T attributes, Func<Ver, string> attributeNameFunc, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>>
    {
        var attributeName = attributeNameFunc(SemanticConventionVersion);

        // if attributeName is empty or there is no value, exit
        if (string.IsNullOrEmpty(attributeName) ||
           (string.IsNullOrEmpty(value?.ToString()) && !addIfNull))
        {
            return attributes;
        }

        attributes.Add(new(attributeName, value));

        return attributes;
    }

    private static Activity? SetTag(this Activity? activity, Func<Ver, string> attributeNameFunc, object? value)
    {
        var attributeName = attributeNameFunc(SemanticConventionVersion);

        activity?.SetTag(attributeName, value);

        return activity;
    }

    public static IDictionary<string, string> AddDic(IDictionary<string, string> dict, Func<Ver, string> attributeNameFunc, string value)
    {
        var attributeName = attributeNameFunc(SemanticConventionVersion);

        if (!string.IsNullOrEmpty(attributeName))
        {
            dict.Add(attributeName, value);
        }

        return dict;
    }
}
