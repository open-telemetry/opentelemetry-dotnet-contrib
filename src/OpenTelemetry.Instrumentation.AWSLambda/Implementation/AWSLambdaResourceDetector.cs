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
        var resourceAttributes = new List<KeyValuePair<string, object>>
        {
            new(AWSLambdaSemanticConventions.AttributeCloudProvider, AWSLambdaUtils.GetCloudProvider()),
            new(AWSLambdaSemanticConventions.AttributeCloudRegion, AWSLambdaUtils.GetAWSRegion()),
            new(AWSLambdaSemanticConventions.AttributeFaasName, AWSLambdaUtils.GetFunctionName()),
            new(AWSLambdaSemanticConventions.AttributeFaasVersion, AWSLambdaUtils.GetFunctionVersion()),
        };

        return new Resource(resourceAttributes);
    }
}
