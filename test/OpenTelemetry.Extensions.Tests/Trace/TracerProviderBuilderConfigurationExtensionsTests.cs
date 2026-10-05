// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class TracerProviderBuilderConfigurationExtensionsTests
{
    [Fact]
    public void AddSourcesFromConfiguration_Success()
    {
        var values = new Dictionary<string, string?>
        {
            ["OpenTelemetry:Tracing:Sources:0"] = "SourceA",
            ["OpenTelemetry:Tracing:Sources:1"] = "SourceB",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSourcesFromConfiguration(configuration.GetSection("OpenTelemetry:Tracing:Sources"))
            .Build();

        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddSourcesFromConfiguration_NullBuilder_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddSourcesFromConfiguration(configuration.GetSection("Sources")));
    }

    [Fact]
    public void AddSourcesFromConfiguration_NullSection_Throws()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddSourcesFromConfiguration(null!));
    }

    [Fact]
    public void AddSourcesFromConfiguration_EmptyOrWhitespaceSource_Throws()
    {
        var values = new Dictionary<string, string?>
        {
            ["Sources:0"] = "SourceA",
            ["Sources:1"] = " ",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.AddSourcesFromConfiguration(configuration.GetSection("Sources")));
        Assert.Contains("Sources:1", ex.Message);
    }
}
