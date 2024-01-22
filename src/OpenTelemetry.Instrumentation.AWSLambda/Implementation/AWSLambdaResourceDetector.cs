// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
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

        if (AWSLambdaUtils.GetAWSRegion() is { Length: > 0 } region)
        {
            resourceAttributes.Add(new(AWSLambdaSemanticConventions.AttributeCloudRegion, region));
        }

        if (AWSLambdaUtils.GetFunctionName() is { Length: > 0 } functionName)
        {
            resourceAttributes.Add(new(AWSLambdaSemanticConventions.AttributeFaasName, functionName));
        }

        if (AWSLambdaUtils.GetFunctionVersion() is { Length: > 0 } functionVersion)
        {
            resourceAttributes.Add(new(AWSLambdaSemanticConventions.AttributeFaasVersion, functionVersion));
        }

        return new Resource(resourceAttributes);
    }
}
