// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Google.Api.Gax.Grpc;
using Google.Cloud.Trace.V2;
using Grpc.Core;

namespace OpenTelemetry.Exporter.Stackdriver.Tests;

internal class TestTraceServiceClient(bool throwException) : TraceServiceClient
{
    private readonly bool throwException = throwException;

    public List<Span> Spans { get; } = new List<Span>();

    public override void BatchWriteSpans(BatchWriteSpansRequest request, CallSettings callSettings)
    {
        if (this.throwException)
        {
            throw new RpcException(Status.DefaultCancelled);
        }

        this.Spans.AddRange(request.Spans);
    }
}
