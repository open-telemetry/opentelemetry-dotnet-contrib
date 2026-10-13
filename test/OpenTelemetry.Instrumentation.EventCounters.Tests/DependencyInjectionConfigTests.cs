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

        await using var sp = services.BuildServiceProvider();

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
        }
    }

    [Fact]
    public async Task TestMetricsOptionsWithCallbackOverridesDi()
    {
        var diCallbackInvoked = false;
        var callbackInvoked = false;
        int? observedIntervalInCallback = null;

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
                    observedIntervalInCallback = options.RefreshIntervalSecs;
                    options.RefreshIntervalSecs = 5;
                }));

        await using var sp = services.BuildServiceProvider();

        try
        {
            foreach (var hostedService in sp.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            Assert.True(diCallbackInvoked);
            Assert.True(callbackInvoked);
            Assert.Equal(2, observedIntervalInCallback);
        }
        finally
        {
            foreach (var hostedService in sp.GetServices<IHostedService>().Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
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

        await using var sp = services.BuildServiceProvider();

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
        }
    }
}
