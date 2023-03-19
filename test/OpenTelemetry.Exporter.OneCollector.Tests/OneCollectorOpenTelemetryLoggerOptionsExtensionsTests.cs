// <copyright file="OneCollectorOpenTelemetryLoggerOptionsExtensionsTests.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Exporter.OneCollector.Tests;

public class OneCollectorOpenTelemetryLoggerOptionsExtensionsTests
{
    [Fact]
    public void ConfigureExporterTest()
    {
        OneCollectorExporter<LogRecord>? exporterInstance = null;

        using var loggerFactory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(builder =>
            {
                builder.AddOneCollectorExporter(
                    "InstrumentationKey=token-extrainformation",
                    configure => configure.ConfigureExporter(exporter => exporterInstance = exporter));
            }));

        Assert.NotNull(exporterInstance);

        using var payloadTransmittedRegistration = exporterInstance.RegisterPayloadTransmittedCallback(OnPayloadTransmitted);

        Assert.NotNull(payloadTransmittedRegistration);

        static void OnPayloadTransmitted(in OneCollectorExporterPayloadTransmittedCallbackArguments args)
        {
        }
    }

    [Fact]
    public void InstrumentationKeyAndTenantTokenValidationTest()
    {
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(builder =>
                {
                    builder.AddOneCollectorExporter("InstrumentationKey=token-extrainformation");
                }));
        }

        {
            using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(builder =>
                {
                    builder.AddOneCollectorExporter(configure => configure.SetConnectionString("InstrumentationKey=token-extrainformation"));
                }));
        }

        Assert.Throws<OneCollectorExporterValidationException>(() =>
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(builder =>
                {
                    builder.AddOneCollectorExporter(configure => { });
                }));
        });

        Assert.Throws<OneCollectorExporterValidationException>(() =>
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(builder =>
                {
                    builder.AddOneCollectorExporter("InstrumentationKey=invalidinstrumentationkey");
                }));
        });

        Assert.Throws<OneCollectorExporterValidationException>(() =>
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(builder =>
                {
                    builder.AddOneCollectorExporter("UnknownKey=invalidinstrumentationkey");
                }));
        });
    }
}
