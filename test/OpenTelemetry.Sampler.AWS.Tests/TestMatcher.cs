// <copyright file="TestMatcher.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using Xunit;

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
    public void TestWildcardMatch(string input, string pattern)
    {
        Assert.True(Matcher.WildcardMatch(input, pattern));
    }

    [Theory]
    [InlineData(null, "Hello*")]
    [InlineData("HelloWorld", null)]
    public void TestWildcardDoesNotMatch(string input, string pattern)
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
    public void TestAttributeMatchingWithoutSpanTags()
    {
        var ruleAttributes = new Dictionary<string, string>
        {
            { "dog", "bar?" },
            { "cow", "mooo" },
        };

        Assert.False(Matcher.AttributeMatch(new List<KeyValuePair<string, object?>>(), ruleAttributes));
        Assert.False(Matcher.AttributeMatch(null, ruleAttributes));
    }
}
