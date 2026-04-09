// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal static class MeterFactory
{
    /// <summary>
    /// Creates a new <see cref="Meter"/> for the assembly associated
    /// with the specified type and semantic conventions version.
    /// </summary>
    /// <typeparam name="T">The type for which the <see cref="Meter"/> is created for its assembly.</typeparam>
    /// <param name="semanticConventionsVersion">The version of the semantic conventions.</param>
    /// <returns>A new <see cref="Meter"/> instance.</returns>
    public static Meter Create<T>(Version semanticConventionsVersion)
        => Create(typeof(T), semanticConventionsVersion);

    /// <summary>
    /// Creates a new <see cref="Meter"/> for the assembly associated
    /// with the specified type and semantic conventions version.
    /// </summary>
    /// <param name="type">The type for which the <see cref="Meter"/> is created for its assembly.</param>
    /// <param name="semanticConventionsVersion">The version of the semantic conventions.</param>
    /// <returns>A new <see cref="Meter"/> instance.</returns>
    public static Meter Create(Type type, Version semanticConventionsVersion)
    {
        Guard.ThrowIfNull(type);
        Guard.ThrowIfNull(semanticConventionsVersion);

        string telemetrySchemaUrl = $"https://opentelemetry.io/schemas/{semanticConventionsVersion.ToString(3)}";

        var assembly = type.Assembly;
        var assemblyName = assembly.GetName();
#pragma warning disable IDE0370 // Suppression is unnecessary
        var name = assemblyName.Name!;
#pragma warning restore IDE0370 // Suppression is unnecessary
        var version = assembly.GetPackageVersion();

        var options = new MeterOptions(name)
        {
            TelemetrySchemaUrl = telemetrySchemaUrl,
            Version = version,
        };

        return new(options);
    }
}
