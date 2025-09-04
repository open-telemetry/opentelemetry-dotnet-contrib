// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.Enrichment.Http.Tests;

internal sealed class AcceptanceTestEnricher : HttpClientTraceEnricher
{
    public override void EnrichWithRequest(
        in TraceEnrichmentBag bag,
        HttpRequestMessage request) =>
        bag.Add(
            "accept.request.method",
            request.Method.Method);

    public override void EnrichWithResponse(
        in TraceEnrichmentBag bag,
        HttpResponseMessage response) =>
        bag.Add(
            "accept.response.status",
            (int)response.StatusCode);

    public override void EnrichWithException(
        in TraceEnrichmentBag bag,
        Exception exception) =>
        bag.Add(
            "accept.exception",
            exception.GetType().Name);
}
