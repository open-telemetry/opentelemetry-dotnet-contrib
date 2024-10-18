// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation;

/// <summary>
/// Semantic conventions for AWS Lambda.
/// </summary>
internal static class AWSLambdaSemanticConventions
{
    public const string AttributeCloudAccountID = CloudAttributes.AttributeCloudAccountId;
    public const string AttributeCloudProvider = CloudAttributes.AttributeCloudProvider;
    public const string AttributeCloudRegion = CloudAttributes.AttributeCloudRegion;
    public const string AttributeFaasExecution = FaasAttributes.AttributeFaasInvocationId;
    public const string AttributeFaasID = CloudAttributes.AttributeCloudResourceId;
    public const string AttributeFaasName = FaasAttributes.AttributeFaasName;
    public const string AttributeFaasVersion = FaasAttributes.AttributeFaasVersion;
    public const string AttributeFaasTrigger = FaasAttributes.AttributeFaasTrigger;
    public const string AttributeFaasColdStart = FaasAttributes.AttributeFaasColdstart;
}
