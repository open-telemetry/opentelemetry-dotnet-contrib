// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;

namespace OpenTelemetry.Sampler.AWS.FuzzTests;

public static class MatcherGenerators
{
    private static readonly char[] TextAlphabet = ['a', 'b', 'A', 'B', '.', '/', '\n'];
    private static readonly char[] PatternAlphabet = ['a', 'b', 'A', 'B', '.', '/', '*', '?', '*', '?'];

    private static readonly string[] AttributeKeys = ["k0", "k1", "k2", "k3", "k4"];
    private static readonly string[] AttributePatterns = ["*", "v", "V", "v*", "?", "*w", "vv", "??"];
    private static readonly string[] AttributeValues = ["v", "vv", "w", "vw", string.Empty];

    public static Arbitrary<GlobCase> GlobCaseArbitrary()
    {
        var gen =
            from text in StringOf(TextAlphabet, 0, 12)
            from pattern in StringOf(PatternAlphabet, 0, 8)
            select new GlobCase(text, pattern);

        return gen.ToArbitrary();
    }

    public static Arbitrary<MatchingGlobCase> MatchingGlobCaseArbitrary()
    {
        var gen =
            from text in StringOf(TextAlphabet, 0, 20)
            from choices in Gen.Choose(0, 4).ListOf(text.Length * 2)
            select new MatchingGlobCase(text, DerivePattern(text, [.. choices]));

        return gen.ToArbitrary();
    }

    public static Arbitrary<AttributeCase> AttributeCaseArbitrary()
    {
        var ruleGen =
            from keyCount in Gen.Choose(0, 3)
            from keys in Gen.Elements(AttributeKeys).ListOf(keyCount)
            from patterns in Gen.Elements(AttributePatterns).ListOf(keyCount)
            select keys.Zip(patterns, (k, p) => new KeyValuePair<string, string>(k, p))
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g.First().Value);

        var tagsGen =
            from tagCount in Gen.Choose(0, 8)
            from keys in Gen.Elements(AttributeKeys).ListOf(tagCount)
            from values in Gen.Elements(AttributeValues).Select(v => (string?)v).ListOf(tagCount)
            from nullMask in Gen.Choose(0, 7).ListOf(tagCount)
            select Normalize(keys, values, nullMask);

        var gen =
            from rule in ruleGen
            from tags in tagsGen
            select new AttributeCase(tags, rule);

        return gen.ToArbitrary();
    }

    private static Gen<string> StringOf(char[] alphabet, int minLength, int maxLength) =>
        from length in Gen.Choose(minLength, maxLength)
        from chars in Gen.Elements(alphabet).ListOf(length)
        select new string([.. chars]);

    private static string DerivePattern(string text, int[] choices)
    {
        // Every step keeps the pattern matching the text: a character is kept, matched by '?',
        // absorbed into a '*', or has a '*' (which may match nothing) inserted before it.
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            switch (choices[i * 2])
            {
                case 1:
                    builder.Append('?');
                    break;
                case 2:
                    builder.Append('*');
                    break;
                default:
                    builder.Append(text[i]);
                    break;
            }

            if (choices[(i * 2) + 1] == 0)
            {
                builder.Append('*');
            }
        }

        return builder.ToString();
    }

    private static List<KeyValuePair<string, object?>> Normalize(
        List<string> keys,
        List<string?> values,
        List<int> nullMask)
    {
        // A repeated key is given the value of its first occurrence.
        var firstValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        var tags = new List<KeyValuePair<string, object?>>();

        for (var i = 0; i < keys.Count; i++)
        {
            if (!firstValues.TryGetValue(keys[i], out var value))
            {
                value = nullMask[i] == 0 ? null : values[i];
                firstValues[keys[i]] = value;
            }

            tags.Add(new KeyValuePair<string, object?>(keys[i], value));
        }

        return tags;
    }
}
