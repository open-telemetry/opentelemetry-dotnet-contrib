// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests;

public class DependencyInjectionConfigTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("CustomName")]
    public async Task TestMetricsOptionsDiConfig(string? name)
    {
        var optionsPickedFromDi = false;

        var services = new ServiceCollection();

        services
            .Configure<EventCountersInstrumentationOptions>(name, _ => optionsPickedFromDi = true)
            .AddOpenTelemetry()
            .WithMetrics(builder =>
                builder.AddEventCountersInstrumentation(name, configure: null));

        var sp = services.BuildServiceProvider();

        try
        {
            foreach (var hostedService in sp.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            Assert.True(optionsPickedFromDi);
        }
        finally
        {
            foreach (var hostedService in sp.GetServices<IHostedService>().Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }

            await sp.DisposeAsync();
        }
    }

    [Fact]
    public async Task TestMetricsOptionsWithCallbackOverridesDi()
    {
        var diCallbackInvoked = false;
        var callbackInvoked = false;

        var services = new ServiceCollection();

        services
            .Configure<EventCountersInstrumentationOptions>(options =>
            {
                diCallbackInvoked = true;
                options.RefreshIntervalSecs = 2;
            })
            .AddOpenTelemetry()
            .WithMetrics(builder =>
                builder.AddEventCountersInstrumentation(options =>
                {
                    callbackInvoked = true;
                    options.RefreshIntervalSecs = 5;
                }));

        var sp = services.BuildServiceProvider();

        try
        {
            foreach (var hostedService in sp.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            Assert.True(diCallbackInvoked);
            Assert.True(callbackInvoked);
        }
        finally
        {
            foreach (var hostedService in sp.GetServices<IHostedService>().Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }

            await sp.DisposeAsync();
        }
    }

    [Fact]
    public async Task TestMetricsOptionsDefaultOverloadWithDi()
    {
        var diCallbackInvoked = false;

        var services = new ServiceCollection();

        services
            .Configure<EventCountersInstrumentationOptions>(options =>
            {
                diCallbackInvoked = true;
                options.RefreshIntervalSecs = 3;
            })
            .AddOpenTelemetry()
            .WithMetrics(builder =>
                builder.AddEventCountersInstrumentation());

        var sp = services.BuildServiceProvider();

        try
        {
            foreach (var hostedService in sp.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            Assert.True(diCallbackInvoked);
        }
        finally
        {
            foreach (var hostedService in sp.GetServices<IHostedService>().Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }

            await sp.DisposeAsync();
        }
    }
}
