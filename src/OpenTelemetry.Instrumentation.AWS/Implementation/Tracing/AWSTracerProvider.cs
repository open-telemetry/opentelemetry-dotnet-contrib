// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Amazon.Runtime.Telemetry.Tracing;
using OpenTelemetry.AWS;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Tracing;

internal sealed class AWSTracerProvider(SemanticConventionVersion version) : TracerProvider
{
    private readonly ConcurrentDictionary<string, AWSTracer> tracers = new();
    private readonly Version semanticConventionVersion = AWSSemanticConventions.GetVersion(version);

    public override Tracer GetTracer(string scope)
    {
        if (!this.tracers.TryGetValue(scope, out var awsTracer))
        {
#if NET
            awsTracer = this.tracers.GetOrAdd(scope, static (name, version) => CreateTracer(name, version), this.semanticConventionVersion);
#else
            awsTracer = this.tracers.GetOrAdd(scope, (name) => CreateTracer(name, this.semanticConventionVersion));
#endif
        }

        return awsTracer;

        static AWSTracer CreateTracer(string name, Version version)
        {
            return new AWSTracer(Trace.ActivitySourceFactory.Create(typeof(AWSTracerProvider), version, name));
        }
    }
}
