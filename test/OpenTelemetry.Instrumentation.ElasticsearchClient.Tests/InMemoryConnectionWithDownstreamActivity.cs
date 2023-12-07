// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;

namespace OpenTelemetry.Instrumentation.ElasticsearchClient.Tests;

public class InMemoryConnectionWithDownstreamActivity : InMemoryConnection
{
    internal static readonly ActivitySource ActivitySource = new ActivitySource("Downstream");
    internal static readonly ActivitySource NestedActivitySource = new ActivitySource("NestedDownstream");

    public override Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
    {
        using var a1 = ActivitySource.StartActivity("downstream");
        using var a2 = NestedActivitySource.StartActivity("nested-downstream");

        return base.RequestAsync<TResponse>(requestData, cancellationToken);
    }
}
