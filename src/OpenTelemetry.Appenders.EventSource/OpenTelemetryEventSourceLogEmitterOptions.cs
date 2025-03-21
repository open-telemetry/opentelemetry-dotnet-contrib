// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains options that apply to log messages written through the <see
/// cref="OpenTelemetryEventSourceLogEmitter"/>.
/// </summary>
public class OpenTelemetryEventSourceLogEmitterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not formatted log message
    /// should be included on generated <see cref="LogRecord"/>s. Default
    /// value: <see langword="false"/>.
    /// </summary>
    public bool IncludeFormattedMessage { get; set; }
}
