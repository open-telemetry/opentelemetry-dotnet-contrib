// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Amazon.Runtime.Telemetry.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Tracing;

internal sealed class AWSTracerProvider(SemanticConventionVersion version) : TracerProvider
{
    private static readonly string ActivitySourceVersion = GetActivitySourceVersion();

    private readonly ConcurrentDictionary<string, AWSTracer> tracers = new();
    private readonly string telemetrySchemaUrl = GetTelemetrySchemaUrl(version);

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

    private static string GetTelemetrySchemaUrl(SemanticConventionVersion version)
    {
        var versionString = GetSemanticConventionVersion(version);
        return $"https://opentelemetry.io/schemas/{versionString}";
    }

    private static string GetSemanticConventionVersion(SemanticConventionVersion version) => version switch
    {
        SemanticConventionVersion.Latest or SemanticConventionVersion.V1_29_0 => "1.29.0",
        SemanticConventionVersion.V1_28_0 => "1.28.0",
        _ => throw new InvalidEnumArgumentException(nameof(version), (int)version, typeof(SemanticConventionVersion)),
    };
}
