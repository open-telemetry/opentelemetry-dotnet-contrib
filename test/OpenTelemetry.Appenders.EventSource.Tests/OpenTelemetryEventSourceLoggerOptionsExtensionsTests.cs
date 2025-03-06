// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Appenders.EventSource.Tests;

public class OpenTelemetryEventSourceLoggerOptionsExtensionsTests
{
    [Fact]
    public void AddOpenTelemetryEventSourceLogEmitterTest()
    {
        var exportedItems = new List<LogRecord>();

        var services = new ServiceCollection();

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddOpenTelemetry(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });

            loggingBuilder.Services.ConfigureOpenTelemetryLoggerProvider((provider, builder) =>
            {
                builder.AddEventSourceLogEmitter(name =>
                    name == TestEventSource.EventSourceName ? EventLevel.LogAlways : null);
            });
        });

        using (var serviceProvider = services.BuildServiceProvider())
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            TestEventSource.Log.SimpleEvent();
        }

        Assert.Single(exportedItems);
    }
}
