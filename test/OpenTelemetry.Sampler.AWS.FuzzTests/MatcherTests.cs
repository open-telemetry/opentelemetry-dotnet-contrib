// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck;
using FsCheck.Xunit;

namespace OpenTelemetry.Sampler.AWS.FuzzTests;

public static class MatcherTests
{
    private const int MaxTest = 1_000;

    [Property(MaxTest = MaxTest)]
    public static void WildcardMatch_Arbitrary_Input_Does_Not_Throw(string? text, string? pattern) =>
        _ = Matcher.WildcardMatch(text, pattern);

    [Property(MaxTest = MaxTest)]
    public static void WildcardMatch_Star_Matches_Everything(string? text) =>
        Assert.True(Matcher.WildcardMatch(text, "*"));

    [Property(MaxTest = MaxTest)]
    public static void WildcardMatch_Null_Text_Matches_Nothing_But_Star(string? pattern)
    {
        if (pattern == "*")
        {
            return;
        }

        Assert.False(Matcher.WildcardMatch(null, pattern));
    }

    [Property(MaxTest = MaxTest)]
    public static void WildcardMatch_Null_Pattern_Matches_Nothing(string? text) =>
        Assert.False(Matcher.WildcardMatch(text, null));

    [Property(MaxTest = MaxTest)]
    public static void WildcardMatch_Pattern_Without_Wildcards_Is_Case_Insensitive_Equality(string text, string pattern)
    {
        if (pattern.Any(c => c is '*' or '?'))
        {
            return;
        }

        Assert.Equal(
            string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase),
            Matcher.WildcardMatch(text, pattern));
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(MatcherGenerators)])]
    public static void WildcardMatch_Agrees_With_Reference_Implementation(GlobCase value) =>
        Assert.Equal(Reference(value.Text, value.Pattern), Matcher.WildcardMatch(value.Text, value.Pattern));

    [Property(MaxTest = MaxTest)]
    public static void WildcardMatch_Agrees_With_Reference_Implementation_For_Arbitrary_Strings(string? text, string? pattern) =>
        Assert.Equal(Reference(text, pattern), Matcher.WildcardMatch(text, pattern));

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(MatcherGenerators)])]
    public static void WildcardMatch_Matches_A_Pattern_Derived_From_The_Text(MatchingGlobCase value) =>
        Assert.True(Matcher.WildcardMatch(value.Text, value.Pattern), value.ToString());

    [Property(MaxTest = 20)]
    public static void WildcardMatch_Completes_In_Bounded_Time_For_Pathological_Patterns(PositiveInt textLength, PositiveInt starCount)
    {
        // Text that never matches the trailing literal forces every '*' to be retried against every position,
        // which is where a backtracking matcher takes exponential time.
        var text = new string('a', (textLength.Get % 20_000) + 1);
        var pattern = string.Concat(Enumerable.Repeat("*a", (starCount.Get % 200) + 1)) + "b";

        var stopwatch = Stopwatch.StartNew();
        var matched = Matcher.WildcardMatch(text, pattern);
        stopwatch.Stop();

        Assert.False(matched);

        // Linear in text * pattern at worst, which is milliseconds; the bound only has to separate that
        // from exponential behavior.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Took {stopwatch.Elapsed} for a text of {text.Length} and a pattern of {pattern.Length}.");
    }

    [Property(MaxTest = MaxTest)]
    public static void AttributeMatch_Without_Rule_Attributes_Always_Matches(string? key, string? value)
    {
        IEnumerable<KeyValuePair<string, object?>>? tags = key == null
            ? null
            : [new KeyValuePair<string, object?>(key, value)];

        Assert.True(Matcher.AttributeMatch(tags, null));
        Assert.True(Matcher.AttributeMatch(tags, []));
    }

    [Property(MaxTest = MaxTest, Arbitrary = [typeof(MatcherGenerators)])]
    public static void AttributeMatch_Requires_Every_Rule_Attribute_To_Match_A_Tag(AttributeCase value)
    {
        // Every attribute of a rule must be matched by a tag with that key; a key that is repeated
        // among the tags must not be counted once per occurrence as if it were several attributes.
        var expected = value.RuleAttributes.All(rule =>
            value.Tags.Any(tag =>
                string.Equals(tag.Key, rule.Key, StringComparison.Ordinal) &&
                Reference(tag.Value?.ToString(), rule.Value)));

        var actual = Matcher.AttributeMatch(
            value.Tags,
            value.RuleAttributes.ToDictionary(rule => rule.Key, rule => rule.Value, StringComparer.Ordinal));

        Assert.Equal(expected, actual);
    }

    private static bool Reference(string? text, string? pattern)
    {
        if (pattern == "*")
        {
            return true;
        }

        if (text == null || pattern == null)
        {
            return false;
        }

        if (pattern.Length == 0)
        {
            return text.Length == 0;
        }

        if (!pattern.Any(c => c is '*' or '?'))
        {
            return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
        }

        // matches[i][j] is whether the first i characters of the text match the first j characters of the pattern.
        var matches = new bool[text.Length + 1][];
        for (var i = 0; i <= text.Length; i++)
        {
            matches[i] = new bool[pattern.Length + 1];
        }

        matches[0][0] = true;

        for (var j = 1; j <= pattern.Length; j++)
        {
            matches[0][j] = pattern[j - 1] == '*' && matches[0][j - 1];
        }

        for (var i = 1; i <= text.Length; i++)
        {
            for (var j = 1; j <= pattern.Length; j++)
            {
                var p = pattern[j - 1];

                matches[i][j] = p == '*'
                    ? matches[i][j - 1] || matches[i - 1][j]
                    : (p == '?' || p == text[i - 1]) && matches[i - 1][j - 1];
            }
        }

        return matches[text.Length][pattern.Length];
    }
}
