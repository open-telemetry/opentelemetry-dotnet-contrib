// <copyright file="MyAspNetCoreTraceEnricher2.cs" company="OpenTelemetry Authors">
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
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore.Tests;

internal class MyAspNetCoreTraceEnricher2 : AspNetCoreTraceEnricher
{
    public const string RequestKey = "with request 2";
    public const string ResponseKey = "with response 2";

    public int TimesCalledWithRequest { get; private set; }

    public int TimesCalledWithResponse { get; private set; }

    public int TimesCalledWithException { get; private set; }

    public override void EnrichWithHttpRequest(ref TraceEnrichmentBag enrichmentBag, HttpRequest request)
    {
        enrichmentBag.Add(RequestKey, ++this.TimesCalledWithRequest);
    }

    public override void EnrichWithHttpResponse(ref TraceEnrichmentBag enrichmentBag, HttpResponse response)
    {
        enrichmentBag.Add(ResponseKey, ++this.TimesCalledWithResponse);
    }

    public override void EnrichWithException(ref TraceEnrichmentBag enrichmentBag, Exception exception)
    {
        enrichmentBag.Add(nameof(MyAspNetCoreTraceEnricher2), ++this.TimesCalledWithException);
    }
}
