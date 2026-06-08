// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.InfluxDB;

/// <summary>
/// Describes how pending exports are handled when the exporter backpressure limit is reached.
/// </summary>
public enum BackpressureMode
{
    /// <summary>
    /// Wait until space becomes available.
    /// </summary>
    Wait = 0,

    /// <summary>
    /// Drop the newest export.
    /// </summary>
    DropNewest = 1,

    /// <summary>
    /// Drop the oldest queued export when possible; otherwise drop the newest export.
    /// </summary>
    DropOldest = 2,
}
