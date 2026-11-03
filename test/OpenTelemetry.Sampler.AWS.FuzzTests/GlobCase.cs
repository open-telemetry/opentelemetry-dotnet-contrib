// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.AWS.FuzzTests;

/// <summary>
/// A text and a glob pattern drawn from a small alphabet so that matches and near misses are both common.
/// </summary>
public sealed class GlobCase(string text, string pattern)
{
    public string Text { get; } = text;

    public string Pattern { get; } = pattern;

    public override string ToString() => $"Text: \"{this.Text}\", Pattern: \"{this.Pattern}\"";
}
