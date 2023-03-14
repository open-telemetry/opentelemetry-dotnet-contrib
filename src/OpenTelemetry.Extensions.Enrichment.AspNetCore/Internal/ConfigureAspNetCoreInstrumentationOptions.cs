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
using OpenTelemetry.Instrumentation.AspNetCore;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore.Internal;

#pragma warning disable CA1812 // Class is instantiated through dependency injection
internal sealed class ConfigureAspNetCoreInstrumentationOptions : IConfigureOptions<AspNetCoreInstrumentationOptions>
#pragma warning restore CA1812 // Class is instantiated through dependency injection
{
    private readonly AspNetCoreTraceEnrichmentProcessor processor;

    public ConfigureAspNetCoreInstrumentationOptions(AspNetCoreTraceEnrichmentProcessor processor)
    {
        this.processor = processor;
    }

    public void Configure(AspNetCoreInstrumentationOptions options)
    {
        options.EnrichWithHttpRequest = this.processor.EnrichWithHttpRequest;

        options.EnrichWithHttpResponse = this.processor.EnrichWithHttpResponse;

        options.EnrichWithException = this.processor.EnrichWithException;
    }
}
