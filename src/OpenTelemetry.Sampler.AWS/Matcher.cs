// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;

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

        // it is faster to check if we need a regex comparison than
        // doing always regex comparison, even where we may not need it.
        foreach (char c in globPattern)
        {
            if (c == '*' || c == '?')
            {
                return Regex.IsMatch(text, ToRegexPattern(globPattern));
            }
        }

        return string.Equals(text, globPattern, StringComparison.OrdinalIgnoreCase);
    }

    public static bool AttributeMatch(IEnumerable<KeyValuePair<string, object?>>? tags, Dictionary<string, string> ruleAttributes)
    {
        if (ruleAttributes.Count == 0)
        {
            return true;
        }

        if (tags == null)
        {
            return false;
        }

        int matchedCount = 0;

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

        if (matchedCount == ruleAttributes.Count)
        {
            return true;
        }

        return false;
    }

    private static string ToRegexPattern(string globPattern)
    {
        int tokenStart = -1;
        StringBuilder patternBuilder = new StringBuilder();

        for (int i = 0; i < globPattern.Length; i++)
        {
            char c = globPattern[i];
            if (c == '*' || c == '?')
            {
                if (tokenStart != -1)
                {
                    patternBuilder.Append(Regex.Escape(globPattern.Substring(tokenStart, i - tokenStart)));
                    tokenStart = -1;
                }

                if (c == '*')
                {
                    patternBuilder.Append(".*");
                }
                else
                {
                    patternBuilder.Append('.');
                }
            }
            else
            {
                if (tokenStart == -1)
                {
                    tokenStart = i;
                }
            }
        }

        if (tokenStart != -1)
        {
            patternBuilder.Append(Regex.Escape(globPattern.Substring(tokenStart)));
        }

        return patternBuilder.ToString();
    }
}
