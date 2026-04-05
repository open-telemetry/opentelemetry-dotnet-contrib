// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Amazon.Runtime.Telemetry.Tracing;
using OpenTelemetry.AWS;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Tracing;

internal sealed class AWSTracerProvider(SemanticConventionVersion version) : TracerProvider
{
    private static readonly string ActivitySourceVersion = GetActivitySourceVersion();

    private readonly ConcurrentDictionary<string, AWSTracer> tracers = new();
    private readonly string telemetrySchemaUrl = AWSSemanticConventions.GetTelemetrySchemaUrl(version);

    public override Tracer GetTracer(string scope)
    {
        if (!this.tracers.TryGetValue(scope, out var awsTracer))
        {
            awsTracer = this.tracers.GetOrAdd(
                scope,
                (name) =>
                {
                    var options = new ActivitySourceOptions(name)
                    {
                        TelemetrySchemaUrl = this.telemetrySchemaUrl,
                        Version = ActivitySourceVersion,
                    };

                    return new AWSTracer(new ActivitySource(options));
                });
        }

        return awsTracer;
    }

    private static string GetActivitySourceVersion()
    {
        var assembly = typeof(AWSTracerProvider).Assembly;
        return assembly.GetPackageVersion();
    }
}
