// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

/// <summary>
/// KeyValue per spec.
/// </summary>
public class KeyValue
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public AnyValue? Value { get; set; }
}
