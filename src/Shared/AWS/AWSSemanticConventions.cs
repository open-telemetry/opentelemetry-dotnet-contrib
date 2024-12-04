// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.SemanticConventions;

/*
using Ver = OpenTelemetry.AWS.SemanticConventionVersion;

// disable Style Warnings to improve readability of this specific file.
#pragma warning disable SA1124
#pragma warning disable SA1005
#pragma warning disable SA1514
#pragma warning disable SA1201

namespace OpenTelemetry.AWS;

/// <summary>
/// TODO
/// https://opentelemetry.io/docs/specs/otel/versioning-and-stability/
/// </summary>
public enum SemanticConventionVersion
{
    /// <summary>
    /// Pin to the specific state of all Semantic Conventions as of the 1.10 release of this library.
    /// https://github.com/open-telemetry/semantic-conventions/releases/tag/v1.27.0
    /// </summary>
    v1_10_EXPERIMENTAL,

    /// <summary>
    /// Pin to the specific state of all Semantic Conventions as of the 1.11 release of this library.
    /// https://github.com/open-telemetry/semantic-conventions/releases/tag/v1.29.0
    /// </summary>
    v1_11_EXPERIMENTAL,

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
    internal const SemanticConventionVersion DefaultSemanticConventionVersion = Ver.EXPERIMENTAL_UNTIL_STABLE;

    // CLOUD Attributes
    /// <inheritdoc cref="CloudAttributes.AttributeCloudAccountId"/>
    private static readonly Func<Ver, string> AttributeCloudAccountID = (_) => "cloud.account.id";
    /// <inheritdoc cref="CloudAttributes.AttributeCloudAvailabilityZone"/>
    private static readonly Func<Ver, string> AttributeCloudAvailabilityZone = (_) => "cloud.availability_zone";
    /// <inheritdoc cref="CloudAttributes.AttributeCloudPlatform"/>
    private static readonly Func<Ver, string> AttributeCloudPlatform = (_) => "cloud.platform";
    /// <inheritdoc cref="CloudAttributes.AttributeCloudProvider"/>
    private static readonly Func<Ver, string> AttributeCloudProvider = (_) => "cloud.provider";
    /// <inheritdoc cref="CloudAttributes.AttributeCloudRegion"/>
    private static readonly Func<Ver, string> AttributeCloudRegion = (_) => "cloud.region";
    /// <inheritdoc cref="CloudAttributes.AttributeCloudResourceId"/>
    private static readonly Func<Ver, string> AttributeCloudResourceId = (_) => "cloud.resource_id";
    public const string CloudPlatformValuesAwsEc2 = CloudAttributes.CloudPlatformValues.AwsEc2;
    public const string CloudPlatformValuesAwsEcs = CloudAttributes.CloudPlatformValues.AwsEcs;
    public const string CloudPlatformValuesAwsEks = CloudAttributes.CloudPlatformValues.AwsEks;
    public const string CloudPlatformValuesAwsElasticBeanstalk = CloudAttributes.CloudPlatformValues.AwsElasticBeanstalk;
    public const string CloudProviderValuesAws = CloudAttributes.CloudProviderValues.Aws;

    // CONTAINER Attributes
    /// <inheritdoc cref="ContainerAttributes.AttributeContainerId"/>
    private static readonly Func<Ver, string> AttributeContainerID = (_) => "container.id";

    // DB Attributes
    /// <inheritdoc cref="DbAttributes.AttributeDbSystem"/>
    private static readonly Func<Ver, string> AttributeDbSystem = (_) => "db.system";
    /// <inheritdoc cref="DbAttributes.DbSystemValues.Dynamodb"/>
    private static readonly Func<Ver, string> AttributeDynamoDb = (_) => "dynamodb";

    // AWS Attributes
    /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsContainerArn"/>
    private static readonly Func<Ver, string> AttributeEcsContainerArn = (_) => "aws.ecs.container.arn";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsClusterArn"/>
    private static readonly Func<Ver, string> AttributeEcsClusterArn = (_) => "aws.ecs.cluster.arn";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsLaunchtype"/>
    private static readonly Func<Ver, string> AttributeEcsLaunchtype = (_) => "aws.ecs.launchtype";
    /// <inheritdoc cref="AwsAttributes.AwsEcsLaunchtypeValues.Ec2"/>
    private static readonly Func<Ver, string> ValueEcsLaunchTypeEc2 = (_) => "ec2";
    /// <inheritdoc cref="AwsAttributes.AwsEcsLaunchtypeValues.Fargate"/>
    private static readonly Func<Ver, string> ValueEcsLaunchTypeFargate = (_) => "fargate";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsTaskArn"/>
    private static readonly Func<Ver, string> AttributeEcsTaskArn = (_) => "aws.ecs.task.arn";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsTaskFamily"/>
    private static readonly Func<Ver, string> AttributeEcsTaskFamily = (_) => "aws.ecs.task.family";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsEcsTaskRevision"/>
    private static readonly Func<Ver, string> AttributeEcsTaskRevision = (_) => "aws.ecs.task.revision";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsLogGroupNames"/>
    private static readonly Func<Ver, string> AttributeLogGroupNames = (_) => "aws.log.group.names";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsLogGroupArns"/>
    private static readonly Func<Ver, string> AttributeLogGroupArns = (_) => "aws.log.group.arns";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsLogStreamArns"/>
    private static readonly Func<Ver, string> AttributeLogStreamNames = (_) => "aws.log.stream.arns";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsLogStreamNames"/>
    private static readonly Func<Ver, string> AttributeLogStreamArns = (_) => "aws.log.stream.names";
    /// <inheritdoc cref="AwsAttributes.AttributeAwsDynamodbTableNames"/>
    private static readonly Func<Ver, string> AttributeAWSDynamoTableName = (_) => "aws.dynamodb.table_names";
    /// <inheritdoc cref="AwsAttributes."/>
    private static readonly Func<Ver, string> AttributeAWSSQSQueueUrl = (_) => "aws.queue_url"; // todo - confirm in java;
    /// <inheritdoc cref="AwsAttributes."/>
    private static readonly Func<Ver, string> AttributeAWSBedrockAgentId = (_) => "aws.bedrock.agent.id";
    /// <inheritdoc cref="AwsAttributes."/>
    private static readonly Func<Ver, string> AttributeAWSBedrockDataSourceId = (_) => "aws.bedrock.data_source.id";
    /// <inheritdoc cref="AwsAttributes."/>
    private static readonly Func<Ver, string> AttributeAWSBedrockGuardrailId = (_) => "aws.bedrock.guardrail.id";
    /// <inheritdoc cref="AwsAttributes."/>
    private static readonly Func<Ver, string> AttributeAWSBedrockKnowledgeBaseId = (_) => "aws.bedrock.knowledge_base.id";
    /// <inheritdoc cref="AwsAttributes."/>
    private static readonly Func<Ver, string> AttributeAWSBedrock = (v) => v switch
    {
        SemanticConventionVersion.v1_10_EXPERIMENTAL => "aws_bedrock",
        _ => "aws.bedrock", //v1.29
    };

    // FAAS Attributes
    /// <inheritdoc cref="CloudAttributes.AttributeCloudResourceId"/>
    private static readonly Func<Ver, string> AttributeFaasID = (_) => "cloud.resource_id";
    /// <inheritdoc cref="FaasAttributes.AttributeFaasInvocationId"/>
    private static readonly Func<Ver, string> AttributeFaasExecution = (_) => "faas.invocation_id";
    /// <inheritdoc cref="FaasAttributes.AttributeFaasName"/>
    private static readonly Func<Ver, string> AttributeFaasName = (_) => "faas.name";
    /// <inheritdoc cref="FaasAttributes.AttributeFaasVersion"/>
    private static readonly Func<Ver, string> AttributeFaasVersion = (_) => "faas.version";
    /// <inheritdoc cref="FaasAttributes.AttributeFaasTrigger"/>
    private static readonly Func<Ver, string> AttributeFaasTrigger = (_) => "faas.trigger";
    /// <inheritdoc cref="FaasAttributes.AttributeFaasColdstart"/>
    private static readonly Func<Ver, string> AttributeFaasColdStart = (_) => "faas.coldstart";

    // GEN AI Attributes
    /// <inheritdoc cref="GenAiAttributes.AttributeGenAiRequestModel"/>
    private static readonly Func<Ver, string> AttributeGenAiModelId = (_) => "gen_ai.request.model";
    /// <inheritdoc cref="GenAiAttributes.AttributeGenAiSystem"/>
    private static readonly Func<Ver, string> AttributeGenAiSystem = (_) => "gen_ai.system";

    // HOST Attributes
    /// <inheritdoc cref="HostAttributes.AttributeHostId"/>
    private static readonly Func<Ver, string> AttributeHostID = (_) => "host.id";
    /// <inheritdoc cref="HostAttributes.AttributeHostType"/>
    private static readonly Func<Ver, string> AttributeHostType = (_) => "host.type";
    /// <inheritdoc cref="HostAttributes.AttributeHostName"/>
    private static readonly Func<Ver, string> AttributeHostName = (_) => "host.name";

    // Http Attributes
    /// <inheritdoc cref="HttpAttributes.AttributeHttpStatusCode"/>
    private static readonly Func<Ver, string> AttributeHttpStatusCode = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => "http.status_code",
        _ => AttributeHttpResponseStatusCode(v), // replaced with http response status code
    };
    /// <inheritdoc cref="HttpAttributes.AttributeHttpResponseStatusCode"/>
    private static readonly Func<Ver, string> AttributeHttpResponseStatusCode = (_) => "http.response.status_code";
    /// <inheritdoc cref="HttpAttributes.AttributeHttpScheme"/>
    private static readonly Func<Ver, string> AttributeHttpScheme = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => "http.scheme",
        _ => AttributeUrlScheme(v), // replaced with url scheme
    };
    /// <inheritdoc cref="HttpAttributes.AttributeHttpTarget"/>
    private static readonly Func<Ver, string> AttributeHttpTarget = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => "http.target",
        _ => string.Empty, // value no longer written
    };
    /// <inheritdoc cref="HttpAttributes.AttributeHttpMethod"/>
    private static readonly Func<Ver, string> AttributeHttpMethod = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => "http.method",
        _ => AttributeHttpRequestMethod(v), // replaced with http request method
    };
    /// <inheritdoc cref="HttpAttributes.AttributeHttpRequestMethod"/>
    private static readonly Func<Ver, string> AttributeHttpRequestMethod = (_) => "http.request.method";


    // NET Attributes
    /// <inheritdoc cref="NetAttributes.AttributeNetHostName"/>
    private static readonly Func<Ver, string> AttributeNetHostName = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => "net.host.name",
        _ => AttributeServerAddress(v), // replaced with server address
    };
    /// <inheritdoc cref="NetAttributes.AttributeNetHostPort"/>
    private static readonly Func<Ver, string> AttributeNetHostPort = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => "net.host.port",
        _ => AttributeServerPort(v), // replaced with server port
    };

    // SERVER Attributes
    /// <inheritdoc cref="ServerAttributes.AttributeServerAddress"/>
    private static readonly Func<Ver, string> AttributeServerAddress = (_) => "server.address";
    /// <inheritdoc cref="ServerAttributes.AttributeServerPort"/>
    private static readonly Func<Ver, string> AttributeServerPort = (_) => "server.port";

    // K8s Attributes
    /// <inheritdoc cref="K8sAttributes.AttributeK8sClusterName"/>
    private static readonly Func<Ver, string> AttributeK8SClusterName = (_) => "k8s.cluster.name";

    // SERVICE Attributes
    /// <inheritdoc cref="ServiceAttributes.AttributeServiceName"/>
    private static readonly Func<Ver, string> AttributeServiceName = (_) => "service.name";
    /// <inheritdoc cref="ServiceAttributes.AttributeServiceNamespace"/>
    private static readonly Func<Ver, string> AttributeServiceNamespace = (_) => "service.namespace";
    /// <inheritdoc cref="ServiceAttributes.AttributeServiceInstanceId"/>
    private static readonly Func<Ver, string> AttributeServiceInstanceID = (_) => "service.instance.id";
    /// <inheritdoc cref="ServiceAttributes.AttributeServiceVersion"/>
    private static readonly Func<Ver, string> AttributeServiceVersion = (_) => "service.version";
    public static string ServiceNameValuesAwsElasticBeanstalk = "aws_elastic_beanstalk";

    // URL Attributes
    /// <inheritdoc cref="UrlAttributes.AttributeUrlPath"/>
    private static readonly Func<Ver, string> AttributeUrlPath = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => string.Empty, //not used in v1.10
        _ => "url.path",
    };
    /// <inheritdoc cref="UrlAttributes.AttributeUrlQuery"/>
    private static readonly Func<Ver, string> AttributeUrlQuery = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => string.Empty, //not used in v1.10
        _ => "url.query",
    };
    /// <inheritdoc cref="UrlAttributes.AttributeUrlScheme"/>
    private static readonly Func<Ver, string> AttributeUrlScheme = (v) => v switch
    {
        Ver.v1_10_EXPERIMENTAL => string.Empty, //not used in v1.10
        _ => "url.scheme",
    };

    #region Service Parameter Mapping

    /// <inheritdoc cref="AttributeAWSDynamoTableName"/>
    public static IDictionary<string, string> AddAttributeAWSDynamoTableName(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSDynamoTableName, value);
    /// <inheritdoc cref="AttributeAWSSQSQueueUrl"/>
    public static IDictionary<string, string> AddAttributeAWSSQSQueueUrl(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSSQSQueueUrl, value);
    /// <inheritdoc cref="AttributeGenAiModelId"/>
    public static IDictionary<string, string> AddAttributeGenAiModelId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeGenAiModelId, value);
    /// <inheritdoc cref="AttributeAWSBedrockAgentId"/>
    public static IDictionary<string, string> AddAttributeAWSBedrockAgentId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSBedrockAgentId, value);
    /// <inheritdoc cref="AttributeAWSBedrockDataSourceId"/>
    public static IDictionary<string, string> AddAttributeAWSBedrockDataSourceId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSBedrockDataSourceId, value);
    /// <inheritdoc cref="AttributeAWSBedrockGuardrailId"/>
    public static IDictionary<string, string> AddAttributeAWSBedrockGuardrailId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSBedrockGuardrailId, value);
    /// <inheritdoc cref="AttributeAWSBedrockKnowledgeBaseId"/>
    public static IDictionary<string, string> AddAttributeAWSBedrockKnowledgeBaseId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, AttributeAWSBedrockKnowledgeBaseId, value);
    #endregion

    #region Cloud Attributes
    /// <inheritdoc cref="AttributeCloudAccountID"/>
    public static T AddAttributeCloudAccountID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudAccountID, value, addIfNull);
    /// <inheritdoc cref="AttributeCloudAvailabilityZone"/>
    public static T AddAttributeCloudAvailabilityZone<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudAvailabilityZone, value, addIfNull);
    /// <inheritdoc cref="AttributeCloudPlatform"/>
    public static T AddAttributeCloudPlatform<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudPlatform, value, addIfNull);
    /// <inheritdoc cref="AttributeCloudProvider"/>
    public static T AddAttributeCloudProvider<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudProvider, value, addIfNull);
    /// <inheritdoc cref="AttributeCloudRegion"/>
    public static T AddAttributeCloudRegion<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudRegion, value, addIfNull);
    /// <inheritdoc cref="AttributeCloudResourceId"/>
    public static T AddAttributeCloudResourceId<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeCloudResourceId, value, addIfNull);
    #endregion

    #region Container
    /// <inheritdoc cref="AttributeContainerID"/>
    public static T AddAttributeContainerId<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeContainerID, value, addIfNull);
    #endregion

    #region AWS
    /// <inheritdoc cref="AttributeDbSystem"/>
    public static Activity? SetTagAttributeDbSystemToDynamoDb(this Activity? activity)
        => SetTag(activity, AttributeDbSystem, AttributeDynamoDb);
    /// <inheritdoc cref="AttributeGenAiSystem"/>
    public static Activity? SetTagAttributeGenAiSystemToBedrock(this Activity? activity)
        => SetTag(activity, AttributeGenAiSystem, AttributeAWSBedrock);
    /// <inheritdoc cref="AttributeEcsContainerArn"/>
    public static T AddAttributeEcsContainerArn<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsContainerArn, value, addIfNull);
    /// <inheritdoc cref="AttributeEcsClusterArn"/>
    public static T AddAttributeEcsClusterArn<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsClusterArn, value, addIfNull);
    /// <inheritdoc cref="AttributeEcsLaunchtype"/>
    public static T AddAttributeEcsLaunchtype<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsLaunchtype, value, addIfNull);
    /// <inheritdoc cref="AddAttributeEcsLaunchtype{T}"/>
    public static T AddAttributeEcsLaunchtypeIsEc2<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => AddAttributeEcsLaunchtype(attributes, ValueEcsLaunchTypeEc2);
    /// <inheritdoc cref="AddAttributeEcsLaunchtype{T}"/>
    public static T AddAttributeEcsLaunchtypeIsFargate<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => AddAttributeEcsLaunchtype(attributes, ValueEcsLaunchTypeFargate);
    /// <inheritdoc cref="AttributeEcsTaskArn"/>
    public static T AddAttributeEcsTaskArn<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsTaskArn, value, addIfNull);
    /// <inheritdoc cref="AttributeEcsTaskFamily"/>
    public static T AddAttributeEcsTaskFamily<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsTaskFamily, value, addIfNull);
    /// <inheritdoc cref="AttributeEcsTaskRevision"/>
    public static T AddAttributeEcsTaskRevision<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeEcsTaskRevision, value, addIfNull);
    /// <inheritdoc cref="AttributeLogGroupNames"/>
    public static T AddAttributeLogGroupNames<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeLogGroupNames, value, addIfNull);
    /// <inheritdoc cref="AttributeLogGroupArns"/>
    public static T AddAttributeLogGroupArns<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeLogGroupArns, value, addIfNull);
    /// <inheritdoc cref="AttributeLogStreamNames"/>
    public static T AddAttributeLogStreamNames<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeLogStreamNames, value, addIfNull);
    /// <inheritdoc cref="AttributeLogStreamArns"/>
    public static T AddAttributeLogStreamArns<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeLogStreamArns, value, addIfNull);
    #endregion

    #region Faas
    /// <inheritdoc cref="AttributeFaasID"/>
    public static T AddAttributeFaasID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasID, value, addIfNull);
    /// <inheritdoc cref="AttributeFaasExecution"/>
    public static T AddAttributeFaasExecution<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasExecution, value, addIfNull);
    /// <inheritdoc cref="AttributeFaasName"/>
    public static T AddAttributeFaasName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasName, value, addIfNull);
    /// <inheritdoc cref="AttributeFaasVersion"/>
    public static T AddAttributeFaasVersion<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasVersion, value, addIfNull);
    /// <inheritdoc cref="AttributeFaasTrigger"/>
    public static T AddAttributeFaasTrigger<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasTrigger, value, addIfNull);
    /// <inheritdoc cref="AttributeFaasColdStart"/>
    public static T AddAttributeFaasColdStart<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeFaasColdStart, value, addIfNull);

    #endregion

    #region Host
    /// <inheritdoc cref="AttributeHostID"/>
    public static T AddAttributeHostID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHostID, value, addIfNull);
    /// <inheritdoc cref="AttributeHostType"/>
    public static T AddAttributeHostType<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHostType, value, addIfNull);
    /// <inheritdoc cref="AttributeHostName"/>
    public static T AddAttributeHostName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHostName, value, addIfNull);
    #endregion

    #region Http
    /// <inheritdoc cref="AttributeHttpStatusCode"/>
    public static Activity? SetTagAttributeHttpStatusCode(this Activity? activity, int value)
        => SetTag(activity, AttributeHttpStatusCode, value);
    /// <inheritdoc cref="AttributeHttpScheme"/>
    public static T AddAttributeHttpScheme<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHttpScheme, value, addIfNull);
    /// <inheritdoc cref="AttributeHttpTarget"/>
    public static T AddAttributeHttpTarget<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHttpTarget, value, addIfNull);
    /// <inheritdoc cref="AttributeHttpMethod"/>
    public static T AddAttributeHttpMethod<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeHttpMethod, value, addIfNull);
    #endregion

    #region Net
    /// <inheritdoc cref="AttributeNetHostName"/>
    public static T AddAttributeNetHostName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeNetHostName, value, addIfNull);
    /// <inheritdoc cref="AttributeNetHostPort"/>
    public static T AddAttributeNetHostPort<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeNetHostPort, value, addIfNull);
    #endregion

    #region K8s
    /// <inheritdoc cref="AttributeK8SClusterName"/>
    public static T AddAttributeK8SClusterName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeK8SClusterName, value, addIfNull);
    #endregion

    #region Service
    /// <inheritdoc cref="AttributeServiceName"/>
    public static T AddAttributeServiceName<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeServiceName, value, addIfNull);
    /// <inheritdoc cref="AttributeServiceNamespace"/>
    public static T AddAttributeServiceNamespace<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeServiceNamespace, value, addIfNull);
    /// <inheritdoc cref="AttributeServiceInstanceID"/>
    public static T AddAttributeServiceInstanceID<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeServiceInstanceID, value, addIfNull);
    /// <inheritdoc cref="AttributeServiceVersion"/>
    public static T AddAttributeServiceVersion<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeServiceVersion, value, addIfNull);
    #endregion

    #region Url
    /// <inheritdoc cref="AttributeUrlPath"/>
    public static T AddAttributeUrlPath<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeUrlPath, value, addIfNull);
    /// <inheritdoc cref="AttributeServiceName"/>
    public static T AddAttributeUrlQuery<T>(this T attributes, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, AttributeUrlQuery, value, addIfNull);
    #endregion

    private static T Add<T>(this T attributes, Func<Ver, string> attributeNameFunc, object? value, bool addIfNull = false)
        where T : IList<KeyValuePair<string, object>>
    {
        var attributeName = attributeNameFunc(SemanticConventionVersion);

        // if attributeName is empty or there is no value, exit
        if (string.IsNullOrEmpty(attributeName) ||
           (value == null && !addIfNull))
        {
            return attributes;
        }

        attributes.Add(new(attributeName, value ?? string.Empty));

        return attributes;
    }

    private static Activity? SetTag(this Activity? activity, Func<Ver, string> attributeNameFunc, Func<Ver, object?> valueFunc) =>
        SetTag(activity, attributeNameFunc, valueFunc(SemanticConventionVersion));

    private static Activity? SetTag(this Activity? activity, Func<Ver, string> attributeNameFunc, object? value)
    {
        var attributeName = attributeNameFunc(SemanticConventionVersion);

        activity?.SetTag(attributeName, value);

        return activity;
    }

    private static IDictionary<string, string> AddDic(IDictionary<string, string> dict, Func<Ver, string> attributeNameFunc, string value)
    {
        var attributeName = attributeNameFunc(SemanticConventionVersion);

        if (!string.IsNullOrEmpty(attributeName))
        {
            dict.Add(attributeName, value);
        }

        return dict;
    }
}
    */
