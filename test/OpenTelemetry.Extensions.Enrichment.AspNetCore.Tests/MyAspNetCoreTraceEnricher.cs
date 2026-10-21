// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Extensions.Enrichment.AspNetCore.Tests;

internal sealed class MyAspNetCoreTraceEnricher : AspNetCoreTraceEnricher
{
    public const string Key = nameof(MyAspNetCoreTraceEnricher);

    public int RequestCalls { get; private set; }

    public int ResponseCalls { get; private set; }

    public int ExceptionCalls { get; private set; }

    public override void EnrichWithHttpRequest(
        in TraceEnrichmentBag bag,
        HttpRequest request) => bag.Add(Key + ".request", ++this.RequestCalls);

    public override void EnrichWithHttpResponse(
        in TraceEnrichmentBag bag,
        HttpResponse response) => bag.Add(Key + ".response", ++this.ResponseCalls);

    public override void EnrichWithException(
        in TraceEnrichmentBag bag,
        Exception exception) => bag.Add(Key + ".exception", ++this.ExceptionCalls);
}
