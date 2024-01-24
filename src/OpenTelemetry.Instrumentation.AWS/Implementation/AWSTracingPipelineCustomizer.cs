// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

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

        pipeline.AddHandlerBefore<Marshaller>(new AWSTracingPipelineHandler(this.options));
    }
}
