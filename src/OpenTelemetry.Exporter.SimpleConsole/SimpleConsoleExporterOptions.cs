// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter.SimpleConsole;

/// <summary>
/// Options for the SimpleConsoleExporter.
/// </summary>
public class SimpleConsoleExporterOptions
{
    private int traceIdLength = 8;

    /// <summary>
    /// Gets or sets the console to use for output. Defaults to SystemConsole.
    /// </summary>
    public IConsole Console { get; set; } = new SystemConsole();

    /// <summary>
    /// Gets or sets the timestamp format string. If null, no timestamp is output.
    /// </summary>
    public string? TimestampFormat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use UTC timestamps. If false, local time is used.
    /// </summary>
    public bool UseUtcTimestamp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include the trace ID in the output. Defaults to true.
    /// </summary>
    public bool IncludeTraceId { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include span ID in the output. Defaults to false.
    /// </summary>
    public bool IncludeSpanId { get; set; }

    /// <summary>
    /// Gets or sets the length of the trace ID to display. Must be between 1 and 32. Defaults to 8.
    /// </summary>
    public int TraceIdLength
    {
        get => this.traceIdLength;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(this.TraceIdLength), "TraceIdLength must be at least 1");
            }

            if (value > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(this.TraceIdLength), "TraceIdLength cannot exceed 32");
            }

            this.traceIdLength = value;
        }
    }
}
