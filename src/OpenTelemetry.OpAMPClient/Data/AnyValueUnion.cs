// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAMPClient.Data;

/// <summary>
/// Represents a union type that can hold a value of one of several predefined types: integer, boolean, or string.
/// </summary>
public class AnyValueUnion
{
    private AnyValueUnion(AnyValueType type)
    {
        this.Type = type;
    }

    /// <summary>
    /// Gets the integer value associated with this instance.
    /// </summary>
    public int? IntValue { get; private set; }

    /// <summary>
    /// Gets the boolean value indicating the current state or condition.
    /// </summary>
    public bool? BoolValue { get; private set; }

    /// <summary>
    /// Gets the string value associated with this instance.
    /// </summary>
    public string? StringValue { get; private set; }

    /// <summary>
    /// Gets the double value associated with this instance.
    /// </summary>
    public double? DoubleValue { get; private set; }

    /// <summary>
    /// Gets the type of the value represented by this instance.
    /// </summary>
    internal AnyValueType Type { get; private set; }

    internal static AnyValueUnion From(int value) => new(AnyValueType.Int) { IntValue = value };

    internal static AnyValueUnion From(double value) => new(AnyValueType.Double) { DoubleValue = value };

    internal static AnyValueUnion From(bool value) => new(AnyValueType.Bool) { BoolValue = value };

    internal static AnyValueUnion From(string value) => new(AnyValueType.String) { StringValue = value };
}
