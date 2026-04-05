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
#if NET
            awsTracer = this.tracers.GetOrAdd(scope, static (name, schemaUrl) => CreateTracer(name, schemaUrl), this.telemetrySchemaUrl);
#else
            awsTracer = this.tracers.GetOrAdd(scope, (name) => CreateTracer(name, this.telemetrySchemaUrl));
#endif
        }

        return awsTracer;

        static AWSTracer CreateTracer(string name, string telemetrySchemaUrl)
        {
            var options = new ActivitySourceOptions(name)
            {
                TelemetrySchemaUrl = telemetrySchemaUrl,
                Version = ActivitySourceVersion,
            };

            return new AWSTracer(new ActivitySource(options));
        }
    }

    private static string GetActivitySourceVersion()
    {
        var assembly = typeof(AWSTracerProvider).Assembly;
        return assembly.GetPackageVersion();
    }
}
