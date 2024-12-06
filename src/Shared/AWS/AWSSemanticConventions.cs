// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Diagnostics;

// disable Style Warnings to improve readability of this specific file.
#pragma warning disable SA1124
#pragma warning disable SA1005
#pragma warning disable SA1514
#pragma warning disable SA1201
#pragma warning disable SA1623

namespace OpenTelemetry.AWS;

/// <summary>
/// Abstracts the complexities of honoring <see cref="AWS.SemanticConventionVersion"/>.
/// <para/>
/// Classes emitting attributes can use the extension methods in this class to build
/// a List of <see cref="KeyValuePair{K,V}"/>s containing
/// Attribute Name and Value without  needing to know which version of the
/// Semantic Convention to use.
/// <example>
/// Below is a hypothetical example showing how attributes can be constructed.  It is not necessary
/// for this consumer to accommodate differing behavior based on <see cref="SemanticConventionVersion"/>,
/// as that will be handled by the extension methods (ie <see cref="AddAttributeCloudResourceId{T}"/>) themselves.
/// <code>
/// <![CDATA[
/// var resourceAttributes =
///     new List<KeyValuePair<string, object>>()
///         .AddAttributeCloudResourceId(containerArn)
///         .AddAttributeFoo("value 1")
///         .AddAttributeBar("value 2");
/// ]]>
/// </code>
/// </example>
/// </summary>
internal static partial class AWSSemanticConventions
{
    /// <summary>
    /// <para>
    /// Sets the <see cref="SemanticConventionVersion"/> that will be used to resolve attribute names.
    /// </para>
    /// This should be set by an Options class.
    /// </summary>
    public static SemanticConventionVersion SemanticConventionVersion { get; set; } = DefaultSemanticConventionVersion;

    /// <summary>
    /// Options classes should use this value for initialization.
    /// <para />
    /// Example:
    /// <code>
    /// <![CDATA[
    /// public class AWSLambdaInstrumentationOptions
    /// {
    ///    /// <inheritdoc cref="OpenTelemetry.AWS.SemanticConventionVersion"/>
    ///    public SemanticConventionVersion SemanticConventionVersion { get; set; } = AWSSemanticConventions.DefaultSemanticConventionVersion;
    /// }
    /// ]]>
    /// </code>
    /// </summary>
    internal const SemanticConventionVersion DefaultSemanticConventionVersion = SemanticConventionVersion.Latest;

    #region Service Parameter Mapping

    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeAWSDynamoTableName"/>
    public static IDictionary<string, string> AddAttributeAWSDynamoTableName(this IDictionary<string, string> dict, string value)
        => AddDic(dict, x => x.AttributeAWSDynamoTableName, value);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeAWSSQSQueueUrl"/>
    public static IDictionary<string, string> AddAttributeAWSSQSQueueUrl(this IDictionary<string, string> dict, string value)
        => AddDic(dict, x => x.AttributeAWSSQSQueueUrl, value);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeGenAiModelId"/>
    public static IDictionary<string, string> AddAttributeGenAiModelId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, x => x.AttributeGenAiModelId, value);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeAWSBedrockAgentId"/>
    public static IDictionary<string, string> AddAttributeAWSBedrockAgentId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, x => x.AttributeAWSBedrockAgentId, value);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeAWSBedrockDataSourceId"/>
    public static IDictionary<string, string> AddAttributeAWSBedrockDataSourceId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, x => x.AttributeAWSBedrockDataSourceId, value);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeAWSBedrockGuardrailId"/>
    public static IDictionary<string, string> AddAttributeAWSBedrockGuardrailId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, x => x.AttributeAWSBedrockGuardrailId, value);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeAWSBedrockKnowledgeBaseId"/>
    public static IDictionary<string, string> AddAttributeAWSBedrockKnowledgeBaseId(this IDictionary<string, string> dict, string value)
        => AddDic(dict, x => x.AttributeAWSBedrockKnowledgeBaseId, value);
    #endregion

    #region Cloud Attributes
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudAccountID"/>
    public static T AddAttributeCloudAccountID<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudAccountID, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudAvailabilityZone"/>
    public static T AddAttributeCloudAvailabilityZone<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudAvailabilityZone, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudPlatform"/>
    public static T AddAttributeCloudPlatformIsAwsEc2<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudPlatform, x => x.CloudPlatformValuesAwsEc2);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudPlatform"/>
    public static T AddAttributeCloudPlatformIsAwsEcs<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudPlatform, x => x.CloudPlatformValuesAwsEcs);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudPlatform"/>
    public static T AddAttributeCloudPlatformIsAwsEks<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudPlatform, x => x.CloudPlatformValuesAwsEks);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudPlatform"/>
    public static T AddAttributeCloudPlatformIsAwsElasticBeanstalk<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudPlatform, x => x.CloudPlatformValuesAwsElasticBeanstalk);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudProvider"/>
    public static T AddAttributeCloudProviderIsAWS<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudProvider, x => x.CloudProviderValuesAws);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudRegion"/>
    public static T AddAttributeCloudRegion<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudRegion, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeCloudResourceId"/>
    public static T AddAttributeCloudResourceId<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeCloudResourceId, value, addIfEmpty);
    #endregion

    #region Container
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeContainerID"/>
    public static T AddAttributeContainerId<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeContainerID, value, addIfEmpty);
    #endregion

    #region AWS
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeDbSystem"/>
    public static Activity? SetTagAttributeDbSystemToDynamoDb(this Activity? activity)
        => SetTag(activity, x => x.AttributeDbSystem, x => x.AttributeDynamoDb);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeGenAiSystem"/>
    public static Activity? SetTagAttributeGenAiSystemToBedrock(this Activity? activity)
        => SetTag(activity, x => x.AttributeGenAiSystem, x => x.AttributeAWSBedrock);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeEcsContainerArn"/>
    public static T AddAttributeEcsContainerArn<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeEcsContainerArn, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeEcsClusterArn"/>
    public static T AddAttributeEcsClusterArn<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeEcsClusterArn, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeEcsLaunchtype"/>
    public static T AddAttributeEcsLaunchtypeIsEc2<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeEcsLaunchtype, x => x.ValueEcsLaunchTypeEc2);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeEcsLaunchtype"/>
    public static T AddAttributeEcsLaunchtypeIsFargate<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeEcsLaunchtype, x => x.ValueEcsLaunchTypeFargate);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeEcsTaskArn"/>
    public static T AddAttributeEcsTaskArn<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeEcsTaskArn, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeEcsTaskFamily"/>
    public static T AddAttributeEcsTaskFamily<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeEcsTaskFamily, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeEcsTaskRevision"/>
    public static T AddAttributeEcsTaskRevision<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeEcsTaskRevision, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeLogGroupNames"/>
    public static T AddAttributeLogGroupNames<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeLogGroupNames, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeLogGroupArns"/>
    public static T AddAttributeLogGroupArns<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeLogGroupArns, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeLogStreamNames"/>
    public static T AddAttributeLogStreamNames<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeLogStreamNames, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeLogStreamArns"/>
    public static T AddAttributeLogStreamArns<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeLogStreamArns, value, addIfEmpty);
    #endregion

    #region Faas
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeFaasID"/>
    public static T AddAttributeFaasID<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeFaasID, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeFaasExecution"/>
    public static T AddAttributeFaasExecution<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeFaasExecution, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeFaasName"/>
    public static T AddAttributeFaasName<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeFaasName, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeFaasVersion"/>
    public static T AddAttributeFaasVersion<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeFaasVersion, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeFaasTrigger"/>
    public static T AddAttributeFaasTrigger<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeFaasTrigger, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeFaasColdStart"/>
    public static T AddAttributeFaasColdStart<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeFaasColdStart, value, addIfEmpty);

    #endregion

    #region Host
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeHostID"/>
    public static T AddAttributeHostID<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeHostID, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeHostType"/>
    public static T AddAttributeHostType<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeHostType, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeHostName"/>
    public static T AddAttributeHostName<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeHostName, value, addIfEmpty);
    #endregion

    #region Http
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeHttpStatusCode"/>
    public static Activity? SetTagAttributeHttpStatusCode(this Activity? activity, int value)
        => SetTag(activity, x => x.AttributeHttpStatusCode, value);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeHttpScheme"/>
    public static T AddAttributeHttpScheme<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeHttpScheme, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeHttpTarget"/>
    public static T AddAttributeHttpTarget<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeHttpTarget, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeHttpMethod"/>
    public static T AddAttributeHttpMethod<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeHttpMethod, value, addIfEmpty);
    #endregion

    #region Net
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeNetHostName"/>
    public static T AddAttributeNetHostName<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeNetHostName, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeNetHostPort"/>
    public static T AddAttributeNetHostPort<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeNetHostPort, value, addIfEmpty);
    #endregion

    #region K8s
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeK8SClusterName"/>
    public static T AddAttributeK8SClusterName<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeK8SClusterName, value, addIfEmpty);
    #endregion

    #region Service
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeServiceName"/>
    public static T AddAttributeServiceNameIsAwsElasticBeanstalk<T>(this T attributes)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeServiceName, x => x.ServiceNameValuesAwsElasticBeanstalk);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeServiceNamespace"/>
    public static T AddAttributeServiceNamespace<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeServiceNamespace, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeServiceInstanceID"/>
    public static T AddAttributeServiceInstanceID<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeServiceInstanceID, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeServiceVersion"/>
    public static T AddAttributeServiceVersion<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeServiceVersion, value, addIfEmpty);
    #endregion

    #region Url
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeUrlPath"/>
    public static T AddAttributeUrlPath<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeUrlPath, value, addIfEmpty);
    /// <inheritdoc cref="AWSSemanticConventionsBase.AttributeServiceName"/>
    public static T AddAttributeUrlQuery<T>(this T attributes, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, x => x.AttributeUrlQuery, value, addIfEmpty);
    #endregion

    private static T Add<T>(this T attributes, Func<AWSSemanticConventionsBase, string> attributeNameFunc, Func<AWSSemanticConventionsBase, string> valueFunc)
        where T : IList<KeyValuePair<string, object>> => Add(attributes, attributeNameFunc, valueFunc(GetSemanticConventionVersion()));

    private static T Add<T>(this T attributes, Func<AWSSemanticConventionsBase, string> attributeNameFunc, object? value, bool addIfEmpty = false)
        where T : IList<KeyValuePair<string, object>>
    {
        var semanticConventionVersionImpl = GetSemanticConventionVersion();

        var attributeName = attributeNameFunc(semanticConventionVersionImpl);

        // if attributeName is empty or there is no value, exit
        if (string.IsNullOrEmpty(attributeName) ||
           (string.IsNullOrEmpty(value?.ToString()) && !addIfEmpty))
        {
            return attributes;
        }

        attributes.Add(new(attributeName, value ?? string.Empty));

        return attributes;
    }

    private static Activity? SetTag(this Activity? activity, Func<AWSSemanticConventionsBase, string> attributeNameFunc, Func<AWSSemanticConventionsBase, object?> valueFunc) =>
        SetTag(activity, attributeNameFunc, valueFunc(GetSemanticConventionVersion()));

    private static Activity? SetTag(this Activity? activity, Func<AWSSemanticConventionsBase, string> attributeNameFunc, object? value)
    {
        var semanticConventionVersionImpl = GetSemanticConventionVersion();

        var attributeName = attributeNameFunc(semanticConventionVersionImpl);

        // if attributeName is empty, exit
        if (string.IsNullOrEmpty(attributeName))
        {
            return activity;
        }

        activity?.SetTag(attributeName, value);

        return activity;
    }

    private static IDictionary<string, string> AddDic(IDictionary<string, string> dict, Func<AWSSemanticConventionsBase, string> attributeNameFunc, string value)
    {
        var semanticConventionVersionImpl = GetSemanticConventionVersion();

        var attributeName = attributeNameFunc(semanticConventionVersionImpl);

        if (!string.IsNullOrEmpty(attributeName))
        {
            dict.Add(value, attributeName);
        }

        return dict;
    }

    private static AWSSemanticConventionsBase GetSemanticConventionVersion()
    {
        switch (SemanticConventionVersion)
        {
            case SemanticConventionVersion.Latest:
            case SemanticConventionVersion.v1_29_0_Experimental:
                return new AWSSemanticConventions_v1_29_0();

            case SemanticConventionVersion.v1_27_0_Experimental:
                return new AWSSemanticConventions_v1_27_0();

            default:
                throw new InvalidEnumArgumentException(
                    argumentName: nameof(SemanticConventionVersion),
                    (int)SemanticConventionVersion,
                    typeof(SemanticConventionVersion));
        }
    }
}
