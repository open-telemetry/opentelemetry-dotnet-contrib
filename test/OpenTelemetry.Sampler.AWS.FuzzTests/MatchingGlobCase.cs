// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.AWS.FuzzTests;

/// <summary>
/// A text and a glob pattern that is derived from the text and so must always match it.
/// </summary>
public sealed class MatchingGlobCase(string text, string pattern)
{
    public string Text { get; } = text;

    public string Pattern { get; } = pattern;

    public override string ToString() => $"Text: \"{this.Text}\", Pattern: \"{this.Pattern}\"";
}
