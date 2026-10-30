// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.AWS.FuzzTests;

/// <summary>
/// The tags of a span and the attribute patterns of a rule.
/// Where a tag key is repeated, every occurrence has the same value so there is only one right answer.
/// </summary>
public sealed class AttributeCase(
    IReadOnlyList<KeyValuePair<string, object?>> tags,
    IReadOnlyDictionary<string, string> ruleAttributes)
{
    public IReadOnlyList<KeyValuePair<string, object?>> Tags { get; } = tags;

    public IReadOnlyDictionary<string, string> RuleAttributes { get; } = ruleAttributes;

    public override string ToString() =>
        $"Tags: [{string.Join(", ", this.Tags.Select(t => $"{t.Key}={t.Value ?? "<null>"}"))}], " +
        $"Rule: [{string.Join(", ", this.RuleAttributes.Select(r => $"{r.Key}={r.Value}"))}]";
}
