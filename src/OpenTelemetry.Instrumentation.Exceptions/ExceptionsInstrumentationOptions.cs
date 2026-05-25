// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

/// <summary>
/// Options for unhandled exception instrumentation.
/// </summary>
public class ExceptionsInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether <see
    /// cref="AppDomain.UnhandledException"/> should be captured.
    /// </summary>
    public bool CaptureUnhandledExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether <see
    /// cref="TaskScheduler.UnobservedTaskException"/> should be captured.
    /// </summary>
    public bool CaptureUnobservedTaskExceptions { get; set; } = true;
}
