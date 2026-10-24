// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;

namespace OpenTelemetry.Sampler.AWS.Tests;

public class TestMatcher
{
    [Theory]
    [InlineData(null, "*")]
    [InlineData("", "*")]
    [InlineData("HelloWorld", "*")]
    [InlineData("HelloWorld", "HelloWorld")]
    [InlineData("HelloWorld", "Hello*")]
    [InlineData("HelloWorld", "*World")]
    [InlineData("HelloWorld", "?ello*")]
    [InlineData("HelloWorld", "Hell?W*d")]
    [InlineData("Hello.World", "*.World")]
    [InlineData("Bye.World", "*.World")]
    [InlineData("HELLOWORLD", "HelloWorld")]
    [InlineData("HelloWorld", "HelloWorld*")]
    [InlineData("HelloWorld", "*HelloWorld*")]
    [InlineData("HelloWorld", "H*o*o*d")]
    [InlineData("HelloWorld", "??????????")]
    [InlineData("Hello\nWorld", "Hello?World")]
    [InlineData("Hello\nWorld", "Hello*")]
    [InlineData("/health", "/health*")]
    [InlineData("/health/ready", "/health*")]
    public void TestWildcardMatch(string? input, string pattern)
    {
        Assert.True(Matcher.WildcardMatch(input, pattern));
    }

    [Theory]
    [InlineData(null, "Hello*")]
    [InlineData("HelloWorld", null)]
    [InlineData("", "?")]
    [InlineData("HelloWorld", "Hello")]
    [InlineData("helloworld", "Hello*")]
    [InlineData("HelloWorld", "?????????")]
    [InlineData("HelloWorld", "???????????")]
    [InlineData("HelloWorld", "*Hello")]
    [InlineData("HelloWorld", "World*")]
    [InlineData("HelloWorld", "?ello")]
    [InlineData("HelloWorld", "H*o*o*x")]
    [InlineData("/api/users/health/orders", "/health*")]
    [InlineData("/api/insurance/health-plans", "/health*")]
    [InlineData("/checkout", "?")]
    public void TestWildcardDoesNotMatch(string? input, string? pattern)
    {
        Assert.False(Matcher.WildcardMatch(input, pattern));
    }

    [Fact]
    public void TestAttributeMatching()
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("dog", "bark"),
            new("cat", "meow"),
            new("cow", "mooo"),
        };

        var ruleAttributes = new Dictionary<string, string>
        {
            { "dog", "bar?" },
            { "cow", "mooo" },
        };

        Assert.True(Matcher.AttributeMatch(tags, ruleAttributes));
    }

    [Fact]
    public void TestAttributeMatchingWithoutRuleAttributes()
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("dog", "bark"),
            new("cat", "meow"),
            new("cow", "mooo"),
        };

        var ruleAttributes = new Dictionary<string, string>();

        Assert.True(Matcher.AttributeMatch(tags, ruleAttributes));
    }

    [Fact]
    public void TestAttributeMatchingWithNullRuleAttributes()
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("dog", "bark"),
        };

        Assert.True(Matcher.AttributeMatch(tags, null));
    }

    [Fact]
    public void TestAttributeMatchingWithoutSpanTags()
    {
        var ruleAttributes = new Dictionary<string, string>
        {
            { "dog", "bar?" },
            { "cow", "mooo" },
        };

        Assert.False(Matcher.AttributeMatch([], ruleAttributes));
        Assert.False(Matcher.AttributeMatch(null, ruleAttributes));
    }

    [Fact]
    public void WildcardMatchWithCatastrophicPatternIsFast()
    {
        var globPattern = "*a*a*a*a*a*a*a*a*b";
        var maliciousInput = new string('a', 100_000);

        var stopwatch = Stopwatch.StartNew();
        var result = Matcher.WildcardMatch(maliciousInput, globPattern);
        stopwatch.Stop();

        Assert.False(result);

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"WildcardMatch took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void WildcardMatchOfLongValueWithMultipleWildcardsIsFast()
    {
        // Realistic globs of per-resource rules, and a value of just under 8 KiB (about the longest request
        // line most front ends accept) that repeats their first segments but never contains the last one.
        string[] globPatterns =
        [
            "/api/*/users/*/orders/*",
            "/api/*/users/*/invoices/*",
            "/api/*/users/*/payments/*",
            "/api/*/users/*/refunds/*",
            "/api/*/users/*/addresses/*",
        ];

        var builder = new StringBuilder();
        while (builder.Length + "/api//users/".Length <= 8000)
        {
            builder.Append("/api//users/");
        }

        var input = builder.ToString();

        var stopwatch = Stopwatch.StartNew();
        foreach (var globPattern in globPatterns)
        {
            Assert.False(Matcher.WildcardMatch(input, globPattern));
        }

        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Matching a {input.Length} character value against {globPatterns.Length} globs took {stopwatch.Elapsed}.");
    }
}
