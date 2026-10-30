// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// The members of a <c>tracestate</c> header which are all valid and have different keys.
/// </summary>
public sealed class TraceStateMembers(IReadOnlyList<KeyValuePair<string, string>> members)
{
    public IReadOnlyList<KeyValuePair<string, string>> Members { get; } = members;

    public string Header => string.Join(",", this.Members.Select(m => $"{m.Key}={m.Value}"));

    public override string ToString() => $"\"{this.Header}\"";
}
