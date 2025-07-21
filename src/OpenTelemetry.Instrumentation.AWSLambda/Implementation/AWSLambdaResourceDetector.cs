// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AWS;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation;

internal sealed class AWSLambdaResourceDetector : IResourceDetector
{
    private readonly AWSSemanticConventions semanticConventionBuilder;

    public AWSLambdaResourceDetector(AWSSemanticConventions semanticConventionBuilder)
    {
        this.semanticConventionBuilder = semanticConventionBuilder;
    }

    /// <summary>
    /// Detect the resource attributes for AWS Lambda.
    /// </summary>
    /// <returns>Detected resource.</returns>
    public Resource Detect()
    {
        var builder = this.semanticConventionBuilder
            .AttributeBuilder
            .AddAttributeCloudProviderIsAWS()
            .AddAttributeCloudRegion(AWSLambdaUtils.GetAWSRegion())
            .AddAttributeFaasName(AWSLambdaUtils.GetFunctionName())
            .AddAttributeFaasVersion(AWSLambdaUtils.GetFunctionVersion());

        // TODO Update semantic conventions to include this
        builder.Add("faas.instance", AWSLambdaUtils.GetFunctionInstance()!);
        builder.Add("faas.max_memory", AWSLambdaUtils.GetFunctionMemorySize()!);

        var resourceAttributes = builder.Build();

        return new Resource(resourceAttributes);
    }
}
