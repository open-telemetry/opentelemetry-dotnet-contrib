// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation;

public class CommonExtensionsTests
{
    [Theory]
    [InlineData("test")]
    [InlineData(443)]
    [InlineData(null)]
    public void AddTagIfNotNull_Tag_CorrectTagsList(object? tag)
    {
        var tags = new List<KeyValuePair<string, object>>();

        tags.AddTagIfNotNull("tagName", tag);

        if (tag != null)
        {
            Assert.Single(tags);
            var actualTag = tags.First();
            Assert.Equal("tagName", actualTag.Key);
            Assert.Equal(tag, actualTag.Value);
        }
        else
        {
            Assert.Empty(tags);
        }
    }
}
