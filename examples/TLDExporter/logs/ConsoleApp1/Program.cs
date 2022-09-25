// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

public class Program
{
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
        .AddOpenTelemetry(loggerOptions =>
        {
            loggerOptions.AddTLDLogExporter(exporterOptions =>
            {
                exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";
                exporterOptions.PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                };

                // exporterOptions.TableNameMappings = new Dictionary<string, string>
                // {
                //    ["*"] = "TLDLog",
                // };
            });
        }));

        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
    }
}
