// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public class SqlClientTraceInstrumentationOptionsTests
{
    [Fact]
    public void ShouldEmitOldAttributesWhenStabilityOptInIsDatabaseDup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseSemanticConventionHelper.SemanticConventionOptInKeyName] = "database/dup" })
            .Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.True(options.EmitOldAttributes);
        Assert.True(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldEmitOldAttributesWhenStabilityOptInIsNotSpecified()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.True(options.EmitOldAttributes);
        Assert.False(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldEmitNewAttributesWhenStabilityOptInIsDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DatabaseSemanticConventionHelper.SemanticConventionOptInKeyName] = "database" })
            .Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.False(options.EmitOldAttributes);
        Assert.True(options.EmitNewAttributes);
    }
}
