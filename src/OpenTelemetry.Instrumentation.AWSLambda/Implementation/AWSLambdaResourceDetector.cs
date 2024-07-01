// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation;

internal sealed class AWSLambdaResourceDetector : IResourceDetector
{
    /// <summary>
    /// Detect the resource attributes for AWS Lambda.
    /// </summary>
    /// <returns>Detected resource.</returns>
    public Resource Detect()
    {
        var resourceAttributes = new List<KeyValuePair<string, object>>(4)
        {
            new(AWSLambdaSemanticConventions.AttributeCloudProvider, AWSLambdaUtils.GetCloudProvider()),
        };

        if (AWSLambdaUtils.GetAWSRegion() is { } region)
        {
            resourceAttributes.Add(new(AWSLambdaSemanticConventions.AttributeCloudRegion, region));
        }

        if (AWSLambdaUtils.GetFunctionName() is { } functionName)
        {
            resourceAttributes.Add(new(AWSLambdaSemanticConventions.AttributeFaasName, functionName));
        }

        if (AWSLambdaUtils.GetFunctionVersion() is { } functionVersion)
        {
            resourceAttributes.Add(new(AWSLambdaSemanticConventions.AttributeFaasVersion, functionVersion));
        }

        return new Resource(resourceAttributes);
    }
}
