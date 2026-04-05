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
        if (!this.meters.TryGetValue(scope, out var meter))
        {
#if NET
            meter = this.meters.GetOrAdd(scope, static (name, state) => CreateMeter(name, state), (this.telemetrySchemaUrl, attributes?.AllAttributes));
#else
            meter = this.meters.GetOrAdd(scope, (name) => CreateMeter(name, (this.telemetrySchemaUrl, attributes?.AllAttributes)));
#endif
        }

        return meter;

        static AWSMeter CreateMeter(string name, (string SchemaUrl, IEnumerable<KeyValuePair<string, object?>>? Attributes) state)
        {
            var options = new System.Diagnostics.Metrics.MeterOptions(name)
            {
                TelemetrySchemaUrl = state.SchemaUrl,
                Version = MeterVersion,
            };

            if (state.Attributes is { } attributes)
            {
                options.Tags = attributes;
            }

            return new AWSMeter(new System.Diagnostics.Metrics.Meter(options));
        }
    }

    private static string GetMeterVersion()
    {
        var assembly = typeof(AWSMeterProvider).Assembly;
        return assembly.GetPackageVersion();
    }
}
