// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Data.Sqlite;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public class DbQueryTextSanitizationTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("provider", null)]
    [InlineData(null, "SELECT ?")]
    [InlineData("provider", "SELECT ?")]
    public void ContextCanBeUsedAsDictionaryKeyWithNullableValues(string? providerName, string? queryText)
    {
        var context = new DbQueryTextSanitizationContext(providerName, queryText, null);
        var equivalent = new DbQueryTextSanitizationContext(
            providerName is null ? null : new string(providerName.ToCharArray()),
            queryText is null ? null : new string(queryText.ToCharArray()),
            null);
        var contexts = new Dictionary<DbQueryTextSanitizationContext, string> { [context] = "cached" };

        Assert.Equal("cached", contexts[equivalent]);
        Assert.True(context == equivalent);
        Assert.False(context != equivalent);
        Assert.True(context.Equals((object)equivalent));

        if (providerName is null && queryText is null)
        {
            Assert.Equal("cached", contexts[default]);
        }
    }

    [Fact]
    public void ContextEqualityUsesCommandIdentity()
    {
        using var command = new SqliteCommand("SELECT 1");
        using var otherCommand = new SqliteCommand("SELECT 1");
        var context = new DbQueryTextSanitizationContext("provider", "SELECT 1", command);
        var equivalent = new DbQueryTextSanitizationContext("provider", "SELECT 1", command);
        var different = new DbQueryTextSanitizationContext("provider", "SELECT 1", otherCommand);
        var contexts = new Dictionary<DbQueryTextSanitizationContext, string> { [context] = "cached" };

        Assert.Equal("cached", contexts[equivalent]);
        Assert.False(context == different);
        Assert.True(context != different);
        Assert.False(contexts.TryGetValue(different, out _));

        command.CommandText = "SELECT 2";

        Assert.Equal("cached", contexts[equivalent]);
    }

    [Fact]
    public void ResultsCanBeUsedAsDictionaryKeysWithNullableValues()
    {
        var results = new Dictionary<DbQueryTextSanitizationResult, string>
        {
            [default] = "unchanged",
            [DbQueryTextSanitizationResult.Suppress] = "suppressed",
            [DbQueryTextSanitizationResult.Sanitized("SELECT ?")] = "sanitized",
            [DbQueryTextSanitizationResult.Sanitized("SELECT ?", "SELECT")] = "summarized",
        };

        Assert.Equal("unchanged", results[DbQueryTextSanitizationResult.NotSanitized]);
        Assert.Equal("suppressed", results[DbQueryTextSanitizationResult.Suppress]);
        Assert.Equal("sanitized", results[DbQueryTextSanitizationResult.Sanitized("SELECT ?")]);
        Assert.Equal("summarized", results[DbQueryTextSanitizationResult.Sanitized("SELECT ?", "SELECT")]);
        Assert.False(results.TryGetValue(DbQueryTextSanitizationResult.Sanitized("select ?"), out _));
        Assert.False(results.TryGetValue(DbQueryTextSanitizationResult.Sanitized("SELECT ?", "select"), out _));

        var sanitized = DbQueryTextSanitizationResult.Sanitized("SELECT ?");
        var equivalent = DbQueryTextSanitizationResult.Sanitized("SELECT ?");

        Assert.True(sanitized == equivalent);
        Assert.False(sanitized != equivalent);
        Assert.True(sanitized.Equals((object)equivalent));
        Assert.False(sanitized == DbQueryTextSanitizationResult.NotSanitized);
        Assert.True(sanitized != DbQueryTextSanitizationResult.Suppress);
    }
}
