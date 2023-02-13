// <copyright file="OneCollectorLogExporterTests.cs" company="OpenTelemetry Authors">
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

public class OneCollectorLogExporterTests
{
    [Fact]
    public void Test()
    {
        using var logFactory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(builder =>
            {
                builder.ParseStateValues = true;
                builder.IncludeScopes = true;
                builder.AddOneCollectorExporter(o =>
                {
                    o.TenantToken = "9c4ff4197e6a4aff94a96824907e3de1";
                    o.InstrumentationKey = "9c4ff4197e6a4aff94a96824907e3de1-dd07ea9c-a968-45fe-8beb-1516d56018d9-7119";
                });
            }));

        var logger = logFactory.CreateLogger<OneCollectorLogExporterTests>();

        using var scope = logger.BeginScope("{appId}", 1);

        logger.LogWarning("Hello world {userId}!", 1234);
    }
}
