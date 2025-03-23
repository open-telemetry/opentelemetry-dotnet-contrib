// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains options that apply to log messages written through the OpenTelemetry Serilog sink.
/// </summary>
public class OpenTelemetrySerilogSinkOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not rendered log message
    /// should be included in the log state. Default value: <see langword="false"/>.
    /// </summary>
    public bool IncludeRenderedMessage { get; set; }
}
