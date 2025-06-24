// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Stackdriver;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering a Stackdriver exporter.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Registers a Stackdriver exporter that will receive <see cref="System.Diagnostics.Activity"/> instances.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="projectId">Project ID to send telemetry to.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder UseStackdriverExporter(
        this TracerProviderBuilder builder,
        string projectId)
    {
        Guard.ThrowIfNull(builder);

#pragma warning disable CA2000 // Dispose objects before losing scope
        var activityExporter = new StackdriverTraceExporter(projectId);

        return builder.AddProcessor(new BatchActivityExportProcessor(activityExporter));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
