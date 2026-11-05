// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// A query string with text before and after it, such as the scheme and path of a URI before it.
/// </summary>
public sealed class EmbeddedQuery(string prefix, string query, string suffix)
{
    public string Prefix { get; } = prefix;

    public string Query { get; } = query;

    public string Suffix { get; } = suffix;

    public string Value => this.Prefix + this.Query + this.Suffix;

    public override string ToString() => $"Prefix: \"{this.Prefix}\", Query: \"{this.Query}\", Suffix: \"{this.Suffix}\"";
}
