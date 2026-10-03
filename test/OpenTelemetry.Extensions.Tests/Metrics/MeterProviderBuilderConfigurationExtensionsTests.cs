// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Metrics;

public class MeterProviderBuilderConfigurationExtensionsTests
{
    [Fact]
    public void AddMetersFromConfiguration_Success()
    {
        var values = new Dictionary<string, string?>
        {
            ["OpenTelemetry:Metrics:Meters:0"] = "MeterA",
            ["OpenTelemetry:Metrics:Meters:1"] = "MeterB",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMetersFromConfiguration(configuration.GetSection("OpenTelemetry:Metrics:Meters"))
            .Build();

        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddMetersFromConfiguration_NullBuilder_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        MeterProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddMetersFromConfiguration(configuration.GetSection("Meters")));
    }

    [Fact]
    public void AddMetersFromConfiguration_NullSection_Throws()
    {
        var builder = Sdk.CreateMeterProviderBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddMetersFromConfiguration(null!));
    }

    [Fact]
    public void AddMetersFromConfiguration_EmptyOrWhitespaceMeter_Throws()
    {
        var values = new Dictionary<string, string?>
        {
            ["Meters:0"] = "MeterA",
            ["Meters:1"] = " ",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var builder = Sdk.CreateMeterProviderBuilder();
        var ex = Assert.Throws<ArgumentException>(() => builder.AddMetersFromConfiguration(configuration.GetSection("Meters")));
        Assert.Contains("Meters:1", ex.Message);
    }
}
