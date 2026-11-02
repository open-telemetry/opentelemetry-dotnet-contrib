// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// The text of a <c>tracestate</c> header which may be well formed or may break the grammar in any of the ways
/// a sender might: bad keys or values, empty or repeated members, odd whitespace and too many members.
/// </summary>
public sealed class TraceStateText(string text)
{
    public string Text { get; } = text;

    public override string ToString() => $"\"{this.Text}\"";
}
