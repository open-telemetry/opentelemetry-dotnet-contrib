// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

/// <summary>
/// AnyValue per spec.
/// </summary>
public class AnyValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnyValue"/> class.
    /// </summary>
    /// <param name="stringValue">Nullable string value.</param>
    public AnyValue(string? stringValue)
    {
        this.StringValue = stringValue;
    }

    /// <summary>
    /// Gets or sets the string value.
    /// </summary>
    public string? StringValue { get; set; }
}
