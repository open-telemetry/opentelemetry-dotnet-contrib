// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ElasticsearchClient.Tests;

public class ElasticsearchClientInstrumentationOptionsTests
{
    [Fact]
    public void ShouldEmitOldAttributesWhenStabilityOptInIsNotSpecified()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = new ElasticsearchClientInstrumentationOptions(configuration);

        Assert.True(options.EmitOldAttributes);
        Assert.False(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldEmitNewAttributesWhenStabilityOptInIsDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new(DatabaseSemanticConventionHelper.SemanticConventionOptInKeyName, "database")])
            .Build();

        var options = new ElasticsearchClientInstrumentationOptions(configuration);

        Assert.False(options.EmitOldAttributes);
        Assert.True(options.EmitNewAttributes);
    }

    [Fact]
    public void ShouldEmitBothAttributesWhenStabilityOptInIsDatabaseDup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new(DatabaseSemanticConventionHelper.SemanticConventionOptInKeyName, "database/dup")])
            .Build();

        var options = new ElasticsearchClientInstrumentationOptions(configuration);

        Assert.True(options.EmitOldAttributes);
        Assert.True(options.EmitNewAttributes);
    }
}
