// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Globalization;
using System.Reflection;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.SimpleConsole;

/// <summary>
/// Simple console exporter for OpenTelemetry logs.
/// </summary>
public class SimpleConsoleExporter : BaseExporter<LogRecord>
{
    private static readonly char[] ExceptionSplitChars = new[] { '\r', '\n' };
    private static readonly PropertyInfo? SeverityProperty = typeof(LogRecord).GetProperty(
        "Severity",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    private readonly object syncLock = new();
    private readonly SimpleConsoleExporterOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleConsoleExporter"/> class.
    /// </summary>
    /// <param name="options">The exporter options.</param>
    public SimpleConsoleExporter(SimpleConsoleExporterOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        var console = this.options.Console;

        foreach (var logRecord in batch)
        {
            lock (this.syncLock)
            {
                var severity = GetSeverityString(logRecord);
                var category = logRecord.CategoryName ?? string.Empty;
                var eventId = logRecord.EventId.Id;

                // Use FormattedMessage if available, otherwise fall back to Body
                var message = !string.IsNullOrEmpty(logRecord.FormattedMessage)
                    ? logRecord.FormattedMessage
                    : logRecord.Body?.ToString() ?? string.Empty;

                // Write timestamp if configured
                if (!string.IsNullOrEmpty(this.options.TimestampFormat))
                {
                    var timestampToFormat = this.options.UseUtcTimestamp
                        ? logRecord.Timestamp.ToUniversalTime()
                        : logRecord.Timestamp.ToLocalTime();

                    var timestamp = timestampToFormat.ToString(
                        this.options.TimestampFormat!,
                        CultureInfo.InvariantCulture);
                    console.Write(timestamp);
                }

                // Write severity in color, then rest of the line in default color
                var originalForeground = console.ForegroundColor;
                var originalBackground = console.BackgroundColor;
                SetSeverityColors(severity, console);
                console.Write(severity);
                console.ForegroundColor = originalForeground;
                console.BackgroundColor = originalBackground;

                // Build the first line with trace/span ID if configured
                var firstLine = $": {category}[{eventId}]";

                if (this.options.IncludeTraceId && logRecord.TraceId != default)
                {
                    var traceIdHex = logRecord.TraceId.ToHexString();
                    var truncatedLength = Math.Min(this.options.TraceIdLength, 32);
                    var displayTraceId = traceIdHex.Substring(0, truncatedLength);

                    // Only show ".." if significantly truncated (length < 30)
                    if (this.options.TraceIdLength < 30)
                    {
                        displayTraceId += "..";
                    }

                    firstLine += $" {displayTraceId}";

                    if (this.options.IncludeSpanId && logRecord.SpanId != default)
                    {
                        firstLine += $"-{logRecord.SpanId.ToHexString()}";
                    }
                }

                console.WriteLine(firstLine);
                console.WriteLine($"      {message}");

                // Output exception details if present, indented
                if (logRecord.Exception != null)
                {
                    var exceptionLines = logRecord
                        .Exception.ToString()
                        .Split(ExceptionSplitChars, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in exceptionLines)
                    {
                        console.WriteLine($"      {line}");
                    }
                }
            }
        }

        return ExportResult.Success;
    }

    /// <summary>
    /// Gets the severity string for a log record.
    /// </summary>
    /// <description>
    /// This method uses reflection to access the internal Severity property if available.
    /// When LogRecord.Severity becomes public, update this method to use it directly.
    /// </description>
    private static string GetSeverityString(LogRecord logRecord)
    {
        // Get the internal Severity property via reflection
        var severityValue = SeverityProperty?.GetValue(logRecord);
        if (severityValue != null)
        {
            // Convert LogRecordSeverity enum to int
            int sevNum = (int)severityValue;

            // OpenTelemetry log severity number ranges:
            // 1-4: Trace, 5-8: Debug, 9-12: Info, 13-16: Warn, 17-20: Error, 21-24: Critical
            return sevNum switch
            {
                >= 1 and <= 4 => "trce",
                >= 5 and <= 8 => "dbug",
                >= 9 and <= 12 => "info",
                >= 13 and <= 16 => "warn",
                >= 17 and <= 20 => "fail",
                >= 21 and <= 24 => "crit",
                _ => "unkn",
            };
        }

        return "unkn";
    }

    private static void SetSeverityColors(string severity, IConsole console)
    {
        switch (severity)
        {
            case "trce":
            case "dbug":
                console.ForegroundColor = ConsoleColor.Gray;
                console.BackgroundColor = ConsoleColor.Black;
                break;
            case "info":
                console.ForegroundColor = ConsoleColor.DarkGreen;
                console.BackgroundColor = ConsoleColor.Black;
                break;
            case "warn":
                console.ForegroundColor = ConsoleColor.Yellow;
                console.BackgroundColor = ConsoleColor.Black;
                break;
            case "fail":
                console.ForegroundColor = ConsoleColor.Black;
                console.BackgroundColor = ConsoleColor.DarkRed;
                break;
            case "crit":
                console.ForegroundColor = ConsoleColor.White;
                console.BackgroundColor = ConsoleColor.DarkRed;
                break;
            default:
                console.ForegroundColor = ConsoleColor.Gray;
                console.BackgroundColor = ConsoleColor.Black;
                break;
        }
    }
}
