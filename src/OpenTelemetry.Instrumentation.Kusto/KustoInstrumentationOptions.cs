// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto;

/// <summary>
/// Options for Kusto instrumentation.
/// </summary>
public class KustoInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable tracing.
    /// </summary>
    public bool EnableTracing { get; set; } = true;
}
