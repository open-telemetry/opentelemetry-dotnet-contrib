// <copyright file="ConfigureAspNetCoreInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Options;
using OpenTelemetry.Extensions.Enrichment.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore;

namespace Microsoft.R9.Extensions.HttpClient.Tracing.Internal;

internal sealed class ConfigureAspNetCoreInstrumentationOptions : IConfigureOptions<AspNetCoreInstrumentationOptions>
{
    private readonly AspNetCoreTraceEnrichmentProcessor processor;

    public ConfigureAspNetCoreInstrumentationOptions(AspNetCoreTraceEnrichmentProcessor processor)
    {
        this.processor = processor;
    }

    public void Configure(AspNetCoreInstrumentationOptions options)
    {
        options.EnrichWithHttpRequest = (activity, request) =>
        {
            this.processor.EnrichWithHttpRequest(activity, request);
        };

        options.EnrichWithHttpResponse = (activity, response) =>
        {
            this.processor.EnrichWithHttpResponse(activity, response);
        };

        options.EnrichWithException = (activity, exception) =>
        {
            this.processor.EnrichWithException(activity, exception);
        };
    }
}
