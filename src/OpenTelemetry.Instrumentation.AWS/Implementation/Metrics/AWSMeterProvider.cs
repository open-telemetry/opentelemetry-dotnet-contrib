// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Amazon.Runtime.Telemetry;
using Amazon.Runtime.Telemetry.Metrics;
using OpenTelemetry.AWS;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AWS.Implementation.Metrics;

internal sealed class AWSMeterProvider(SemanticConventionVersion version) : MeterProvider
{
    private static readonly string MeterVersion = GetMeterVersion();

    private readonly ConcurrentDictionary<string, AWSMeter> meters = new();
    private readonly string telemetrySchemaUrl = AWSSemanticConventions.GetTelemetrySchemaUrl(version);

    public override Meter GetMeter(string scope, Attributes? attributes = null)
    {
        // TODO Passing attributes to the Meter is currently not possible due to version limitations
        // in the dependencies. Since none of the SDK operations utilize attributes at this level,
        // so we will omit the attributes for now.
        // This will be revisited after the release of OpenTelemetry.Extensions.AWS which will
        // update OpenTelemetry core component version(s) to `1.9.0` and allow passing tags to
        // the meter constructor.
        // See https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/4065.

        if (!this.meters.TryGetValue(scope, out var meter))
        {
            meter = this.meters.GetOrAdd(
                scope,
                (name) =>
                {
                    var options = new System.Diagnostics.Metrics.MeterOptions(name)
                    {
                        TelemetrySchemaUrl = this.telemetrySchemaUrl,
                        Version = MeterVersion,
                    };

                    return new AWSMeter(new System.Diagnostics.Metrics.Meter(options));
                });
        }

        return meter;
    }

    private static string GetMeterVersion()
    {
        var assembly = typeof(AWSMeterProvider).Assembly;
        return assembly.GetPackageVersion();
    }
}
