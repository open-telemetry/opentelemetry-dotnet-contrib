// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;
using static OpenTelemetry.Internal.DatabaseSemanticConventionHelper;

namespace OpenTelemetry.Internal.Tests;

public class DatabaseSemanticConventionHelperTests
{
    public static IEnumerable<object[]> TestCases => new List<object[]>
    {
        new object[] { null!,  DatabaseSemanticConvention.Old },
        new object[] { string.Empty,  DatabaseSemanticConvention.Old },
        new object[] { " ",  DatabaseSemanticConvention.Old },
        new object[] { "junk",  DatabaseSemanticConvention.Old },
        new object[] { "none",  DatabaseSemanticConvention.Old },
        new object[] { "NONE",  DatabaseSemanticConvention.Old },
        new object[] { "database",  DatabaseSemanticConvention.New },
        new object[] { "DATABASE",  DatabaseSemanticConvention.New },
        new object[] { "database/dup",  DatabaseSemanticConvention.Dupe },
        new object[] { "DATABASE/DUP",  DatabaseSemanticConvention.Dupe },
        new object[] { "junk,,junk",  DatabaseSemanticConvention.Old },
        new object[] { "junk,JUNK",  DatabaseSemanticConvention.Old },
        new object[] { "junk1,junk2",  DatabaseSemanticConvention.Old },
        new object[] { "junk,database",  DatabaseSemanticConvention.New },
        new object[] { "junk,database , database ,junk",  DatabaseSemanticConvention.New },
        new object[] { "junk,database/dup",  DatabaseSemanticConvention.Dupe },
        new object[] { "junk, database/dup ",  DatabaseSemanticConvention.Dupe },
        new object[] { "database/dup,database",  DatabaseSemanticConvention.Dupe },
        new object[] { "database,database/dup",  DatabaseSemanticConvention.Dupe },
    };

    [Fact]
    public void VerifyFlags()
    {
        var testValue = DatabaseSemanticConvention.Dupe;
        Assert.True(testValue.HasFlag(DatabaseSemanticConvention.Old));
        Assert.True(testValue.HasFlag(DatabaseSemanticConvention.New));

        testValue = DatabaseSemanticConvention.Old;
        Assert.True(testValue.HasFlag(DatabaseSemanticConvention.Old));
        Assert.False(testValue.HasFlag(DatabaseSemanticConvention.New));

        testValue = DatabaseSemanticConvention.New;
        Assert.False(testValue.HasFlag(DatabaseSemanticConvention.Old));
        Assert.True(testValue.HasFlag(DatabaseSemanticConvention.New));
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void VerifyGetSemanticConventionOptIn_UsingEnvironmentVariable(string input, string expectedValue)
    {
        try
        {
            Environment.SetEnvironmentVariable(SemanticConventionOptInKeyName, input);

            var expected = Enum.Parse(typeof(DatabaseSemanticConvention), expectedValue);
            Assert.Equal(expected, GetSemanticConventionOptIn(new ConfigurationBuilder().AddEnvironmentVariables().Build()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SemanticConventionOptInKeyName, null);
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void VerifyGetSemanticConventionOptIn_UsingIConfiguration(string input, string expectedValue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [SemanticConventionOptInKeyName] = input })
            .Build();

        var expected = Enum.Parse(typeof(DatabaseSemanticConvention), expectedValue);
        Assert.Equal(expected, GetSemanticConventionOptIn(configuration));
    }
}
