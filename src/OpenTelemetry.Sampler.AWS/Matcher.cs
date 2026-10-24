// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.AWS;

internal static class Matcher
{
    public static readonly IReadOnlyDictionary<string, string> XRayCloudPlatform = new Dictionary<string, string>()
    {
        { "aws_ec2", "AWS::EC2::Instance" },
        { "aws_ecs", "AWS::ECS::Container" },
        { "aws_eks", "AWS::EKS::Container" },
        { "aws_elastic_beanstalk", "AWS::ElasticBeanstalk::Environment" },
        { "aws_lambda", "AWS::Lambda::Function" },
    };

    public static bool WildcardMatch(string? text, string? globPattern)
    {
        if (globPattern == "*")
        {
            return true;
        }

        if (text == null || globPattern == null)
        {
            return false;
        }

        if (globPattern.Length == 0)
        {
            return text.Length == 0;
        }

        // As in the X-Ray sampler of OpenTelemetry Java, a pattern with wildcards is matched
        // case-sensitively, while a pattern without wildcards is compared case-insensitively.
        foreach (var c in globPattern)
        {
            if (c is '*' or '?')
            {
                return GlobMatch(text, globPattern);
            }
        }

        return string.Equals(text, globPattern, StringComparison.OrdinalIgnoreCase);
    }

    public static bool AttributeMatch(IEnumerable<KeyValuePair<string, object?>>? tags, Dictionary<string, string>? ruleAttributes)
    {
        if (ruleAttributes == null || ruleAttributes.Count == 0)
        {
            return true;
        }

        if (tags == null)
        {
            return false;
        }

        var matchedCount = 0;

        foreach (var tag in tags)
        {
            var textToMatch = tag.Value?.ToString();
            ruleAttributes.TryGetValue(tag.Key, out var globPattern);

            if (globPattern == null)
            {
                continue;
            }

            if (WildcardMatch(textToMatch, globPattern))
            {
                matchedCount++;
            }
        }

        return matchedCount == ruleAttributes.Count;
    }

    private static bool GlobMatch(string text, string globPattern)
    {
        // Matches the whole text against a glob pattern in which '*' matches any sequence of characters
        // (including none) and '?' matches any single character. Only the most recent '*' is ever
        // backtracked to, so the time taken is at most proportional to text.Length * globPattern.Length
        // whatever the input, and no memory is allocated.
        var textIndex = 0;
        var patternIndex = 0;

        // The position of the most recent '*' in the pattern, and of the text it was matched at.
        var starIndex = -1;
        var starTextIndex = 0;

        while (textIndex < text.Length)
        {
            if (patternIndex < globPattern.Length)
            {
                var c = globPattern[patternIndex];

                if (c == '*')
                {
                    // Let the '*' match nothing for now.
                    starIndex = patternIndex++;
                    starTextIndex = textIndex;
                    continue;
                }

                if (c == '?' || c == text[textIndex])
                {
                    patternIndex++;
                    textIndex++;
                    continue;
                }
            }

            if (starIndex < 0)
            {
                return false;
            }

            // Let the most recent '*' match one more character and try the rest of the pattern again.
            patternIndex = starIndex + 1;
            textIndex = ++starTextIndex;
        }

        // All of the text has been matched, so the rest of the pattern matches only if it is all '*'.
        while (patternIndex < globPattern.Length && globPattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == globPattern.Length;
    }
}
