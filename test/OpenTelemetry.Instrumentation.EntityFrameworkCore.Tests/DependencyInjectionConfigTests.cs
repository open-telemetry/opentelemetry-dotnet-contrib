// <copyright file="DependencyInjectionConfigTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

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
            .Configure<EntityFrameworkInstrumentationOptions>(name, _ => optionsPickedFromDi = true)
            .AddOpenTelemetry()
            .WithTracing(builder =>
                builder.AddEntityFrameworkCoreInstrumentation(name, configure: null));

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
