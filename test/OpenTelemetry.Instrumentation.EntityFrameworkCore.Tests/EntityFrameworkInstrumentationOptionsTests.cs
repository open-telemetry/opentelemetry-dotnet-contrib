// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public class EntityFrameworkInstrumentationOptionsTests
{
    [Fact]
    public void ShouldEmitOldAttributesWhenStabilityOptInIsDatabaseDup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseSemanticConventionHelper.SemanticConventionOptInKeyName] = "database/dup" })
            .Build();
        var options = new EntityFrameworkInstrumentationOptions(configuration);
        Assert.True(options.EmitOldAttributes);
        Assert.True(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldEmitOldAttributesWhenStabilityOptInIsNotSpecified()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = new EntityFrameworkInstrumentationOptions(configuration);
        Assert.True(options.EmitOldAttributes);
        Assert.False(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldEmitNewAttributesWhenStabilityOptInIsDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseSemanticConventionHelper.SemanticConventionOptInKeyName] = "database" })
            .Build();
        var options = new EntityFrameworkInstrumentationOptions(configuration);
        Assert.False(options.EmitOldAttributes);
        Assert.True(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldNotEmitDatabaseQueryParametersByDefault()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = new EntityFrameworkInstrumentationOptions(configuration);
        Assert.False(options.SetDbQueryParameters);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void ShouldAssignSetDatabaseQueryParametersFromEnvironmentVariable(string value, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_DOTNET_EXPERIMENTAL_EFCORE_ENABLE_TRACE_DB_QUERY_PARAMETERS"] = value })
            .Build();
        var options = new EntityFrameworkInstrumentationOptions(configuration);
        Assert.Equal(expected, options.SetDbQueryParameters);
    }

    [Fact]
    public void ShouldAssignQueryTextSanitizerByDefault()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = new EntityFrameworkInstrumentationOptions(configuration);
        Assert.NotNull(options.QueryTextSanitizer);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Cosmos")]
    [InlineData("MongoDB.EntityFrameworkCore")]
    [InlineData("Contoso.BusinessLogic.DataAccess.Command")]
    public void DefaultQueryTextSanitizerShouldNotSanitizeProvidersThatAreNotSqlLike(string providerName)
    {
        var options = new EntityFrameworkInstrumentationOptions(new ConfigurationBuilder().Build());
        var context = new DbQuerySanitizationContext(
            providerName,
            "select * from Items where Name = 'secret'",
            command: null);

        var result = options.QueryTextSanitizer!(context);

        Assert.False(result.IsSanitized);
        Assert.Null(result.QueryText);
        Assert.Null(result.QuerySummary);
    }
}
