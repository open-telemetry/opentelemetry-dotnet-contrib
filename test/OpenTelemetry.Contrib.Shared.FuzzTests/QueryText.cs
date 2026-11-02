// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// Text drawn from the characters which give a query string its structure, so that separators,
/// empty keys and values, and repeated equals signs are all common.
/// </summary>
public sealed class QueryText(string text)
{
    public string Text { get; } = text;

    public override string ToString() => $"\"{this.Text}\"";
}
