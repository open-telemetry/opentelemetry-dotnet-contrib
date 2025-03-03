// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Serilog.Events;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains options that control the behavior of the OpenTelemetry Serilog enricher.
/// </summary>
public class OpenTelemetrySerilogEnricherOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not the <see
    /// cref="Activity.TraceStateString"/> for the current <see
    /// cref="Activity"/> should be included on <see cref="LogEvent"/>s as the
    /// "TraceState" property. Default value: <see langword="false"/>.
    /// </summary>
    public bool IncludeTraceState { get; set; }
}
