// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// A key and a value which may or may not be valid in a <c>tracestate</c> header.
/// </summary>
public sealed class TraceStateMember(string key, string value)
{
    public string Key { get; } = key;

    public string Value { get; } = value;

    public override string ToString() => $"Key: \"{this.Key}\", Value: \"{this.Value}\"";
}
