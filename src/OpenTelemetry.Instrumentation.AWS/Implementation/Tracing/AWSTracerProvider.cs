// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Amazon.Runtime.Telemetry.Tracing;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Tracing;

internal sealed class AWSTracerProvider : TracerProvider
{
    private static readonly ConcurrentDictionary<string, ActivitySource> TracersDictionary = new();

    public override Tracer GetTracer(string scope)
    {
        var activitySource = TracersDictionary.GetOrAdd(
            scope,
            static scopeName => new ActivitySource(scopeName));

        return new AWSTracer(activitySource);
    }
}
