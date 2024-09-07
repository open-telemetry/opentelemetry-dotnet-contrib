// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation;

/// <summary>
/// Semantic conventions for AWS Lambda.
/// </summary>
internal static class AWSLambdaSemanticConventions
{
    public const string AttributeCloudAccountID = "cloud.account.id";
    public const string AttributeCloudProvider = "cloud.provider";
    public const string AttributeCloudRegion = "cloud.region";
    public const string AttributeFaasExecution = "faas.execution";
    public const string AttributeFaasID = "faas.id";
    public const string AttributeFaasName = "faas.name";
    public const string AttributeFaasVersion = "faas.version";
    public const string AttributeFaasTrigger = "faas.trigger";
    public const string AttributeFaasColdStart = "faas.coldstart";
}
