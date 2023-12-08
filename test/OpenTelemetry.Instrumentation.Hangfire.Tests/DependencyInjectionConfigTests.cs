// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

public class DependencyInjectionConfigTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("CustomName")]
    public async Task TestTracingOptionsDiConfig(string name)
    {
        bool optionsPickedFromDi = false;

        var services = new ServiceCollection();

        services
            .Configure<HangfireInstrumentationOptions>(name, _ => optionsPickedFromDi = true)
            .AddOpenTelemetry()
            .WithTracing(builder =>
                builder.AddHangfireInstrumentation(name, configure: null));

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
}
