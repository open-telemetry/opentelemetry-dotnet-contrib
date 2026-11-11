// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using static OpenTelemetry.Internal.DatabaseSemanticConventionHelper;

namespace OpenTelemetry.Internal.Tests;

public class DatabaseSemanticConventionHelperTests
{
    public static TheoryData<string, string> TestCases => new()
    {
        { null!, nameof(DatabaseSemanticConvention.Old) },
        { string.Empty, nameof(DatabaseSemanticConvention.Old) },
        { " ", nameof(DatabaseSemanticConvention.Old) },
        { "junk", nameof(DatabaseSemanticConvention.Old) },
        { "none", nameof(DatabaseSemanticConvention.Old) },
        { "NONE", nameof(DatabaseSemanticConvention.Old) },
        { "database", nameof(DatabaseSemanticConvention.New) },
        { "DATABASE", nameof(DatabaseSemanticConvention.New) },
        { "database/dup", nameof(DatabaseSemanticConvention.Dupe) },
        { "DATABASE/DUP", nameof(DatabaseSemanticConvention.Dupe) },
        { "junk,,junk", nameof(DatabaseSemanticConvention.Old) },
        { "junk,JUNK", nameof(DatabaseSemanticConvention.Old) },
        { "junk1,junk2", nameof(DatabaseSemanticConvention.Old) },
        { "junk,database", nameof(DatabaseSemanticConvention.New) },
        { "junk,database , database ,junk", nameof(DatabaseSemanticConvention.New) },
        { "junk,database/dup", nameof(DatabaseSemanticConvention.Dupe) },
        { "junk, database/dup ", nameof(DatabaseSemanticConvention.Dupe) },
        { "database/dup,database", nameof(DatabaseSemanticConvention.Dupe) },
        { "database,database/dup", nameof(DatabaseSemanticConvention.Dupe) },
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
        using (EnvironmentVariableScope.Create(SemanticConventionOptInKeyName, input))
        {
#if NET
            var expected = Enum.Parse<DatabaseSemanticConvention>(expectedValue);
#else
            var expected = Enum.Parse(typeof(DatabaseSemanticConvention), expectedValue);
#endif
            Assert.Equal(expected, GetSemanticConventionOptIn(new ConfigurationBuilder().AddEnvironmentVariables().Build()));
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void VerifyGetSemanticConventionOptIn_UsingIConfiguration(string input, string expectedValue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [SemanticConventionOptInKeyName] = input })
            .Build();

#if NET
        var expected = Enum.Parse<DatabaseSemanticConvention>(expectedValue);
#else
        var expected = Enum.Parse(typeof(DatabaseSemanticConvention), expectedValue);
#endif
        Assert.Equal(expected, GetSemanticConventionOptIn(configuration));
    }
}
