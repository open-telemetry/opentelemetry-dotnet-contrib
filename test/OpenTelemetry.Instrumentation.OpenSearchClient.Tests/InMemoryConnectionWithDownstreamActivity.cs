// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenSearch.Client;
using OpenSearch.Net;

namespace OpenTelemetry.Instrumentation.OpenSearchClient.Tests;

internal class InMemoryConnectionWithDownstreamActivity : InMemoryConnection
{
    internal static readonly ActivitySource ActivitySource = new("Downstream");
    internal static readonly ActivitySource NestedActivitySource = new("NestedDownstream");

    public override Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
    {
        using var a1 = ActivitySource.StartActivity("downstream");
        using var a2 = NestedActivitySource.StartActivity("nested-downstream");

        return base.RequestAsync<TResponse>(requestData, cancellationToken);
    }
}
