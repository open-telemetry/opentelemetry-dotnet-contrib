// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.Enrichment.Http.Tests;

internal sealed class TestEnricher : HttpClientTraceEnricher
{
    public int RequestCount { get; private set; }

    public int ResponseCount { get; private set; }

    public int ExceptionCount { get; private set; }

    public override void EnrichWithRequest(in TraceEnrichmentBag bag, HttpRequestMessage request)
    {
        this.RequestCount++;
        bag.Add("test.request", "ok");
    }

    public override void EnrichWithResponse(in TraceEnrichmentBag bag, HttpResponseMessage response)
    {
        this.ResponseCount++;
        bag.Add("test.response", (int)response.StatusCode);
    }

    public override void EnrichWithException(in TraceEnrichmentBag bag, Exception exception)
    {
        this.ExceptionCount++;
        bag.Add("test.exception", exception.GetType().Name);
    }
}
