// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Wires <see cref="AWSPropagatorPipelineHandler"/> into the AWS <see cref="RuntimePipeline"/>
/// so it can inject trace headers and add request information to the tags.
/// </summary>
internal class AWSTracingPipelineCustomizer : IRuntimePipelineCustomizer
{
    private readonly AWSClientInstrumentationOptions options;

    public AWSTracingPipelineCustomizer(AWSClientInstrumentationOptions options)
    {
        this.options = options;
    }

    public string UniqueName
    {
        get
        {
            return "AWS Tracing Registration Customization";
        }
    }

    public void Customize(Type serviceClientType, RuntimePipeline pipeline)
    {
        if (!typeof(AmazonServiceClient).IsAssignableFrom(serviceClientType))
        {
            return;
        }

        var propagatingPipelineHandler = new AWSPropagatorPipelineHandler(this.options);

        // AWSPropagatorPipelineHandler executes after the AWS SDK has marshalled (ie serialized)
        // the outgoing request object so that it can work with the request's Headers
        pipeline.AddHandlerBefore<RetryHandler>(propagatingPipelineHandler);
    }
}
