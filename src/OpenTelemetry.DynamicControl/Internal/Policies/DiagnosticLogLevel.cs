// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// The severity threshold at or above which the SDK emits its own diagnostic logs.
/// </summary>
internal enum DiagnosticLogLevel
{
    /// <summary>
    /// No valid diagnostic log level has been specified.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Emit every diagnostic log, including the most detailed.
    /// </summary>
    Trace,

    /// <summary>
    /// Emit diagnostic logs useful for debugging and above.
    /// </summary>
    Debug,

    /// <summary>
    /// Emit informational diagnostic logs and above.
    /// </summary>
    Information,

    /// <summary>
    /// Emit diagnostic logs describing an abnormal condition and above.
    /// </summary>
    Warning,

    /// <summary>
    /// Emit diagnostic logs describing a failure and above.
    /// </summary>
    Error,

    /// <summary>
    /// Emit no diagnostic logs.
    /// </summary>
    None,
}
