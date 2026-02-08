// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.SurrealDb.Implementation;

/// <summary>
/// Helper class to hold common properties used by <see cref="SurrealDbDiagnosticListener"/>.
/// </summary>
internal sealed class SurrealDbTelemetryHelper
{
    public const string SurrealDbSystemName = "surrealdb";

    private static readonly (ActivitySource ActivitySource, Meter Meter) Telemetry = CreateTelemetry();
    public static readonly ActivitySource ActivitySource = Telemetry.ActivitySource;
    public static readonly Meter Meter = Telemetry.Meter;

    public static readonly Histogram<double> DbClientOperationDuration = Meter.CreateHistogram(
        "db.client.operation.duration",
        unit: "s",
        description: "Duration of database client operations.",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10],
        }
    );

    private static (ActivitySource ActivitySource, Meter Meter) CreateTelemetry()
    {
        const string telemetrySchemaUrl = "https://opentelemetry.io/schemas/1.33.0";
        var assembly = typeof(SurrealDbTelemetryHelper).Assembly;
        var assemblyName = assembly.GetName();
        var name = assemblyName.Name!;
        var version = assembly.GetPackageVersion();

        var activitySourceOptions = new ActivitySourceOptions(name)
        {
            Version = version,
            TelemetrySchemaUrl = telemetrySchemaUrl,
        };

        var meterOptions = new MeterOptions(name)
        {
            Version = version,
            TelemetrySchemaUrl = telemetrySchemaUrl,
        };

        return (new ActivitySource(activitySourceOptions), new Meter(meterOptions));
    }
}
