// <copyright file="AWSTracingPipelineCustomizer.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

        pipeline.AddHandlerBefore<RetryHandler>(new AWSTracingPipelineHandler(this.options));
    }
}
