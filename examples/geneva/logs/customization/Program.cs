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

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

public class Program
{
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            // sets up OpenTelemetry logs for Information and above. * refers to all categories.
            builder.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Information);
            builder.AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddGenevaLogExporter(exporterOptions =>
                {
                    exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";

                    exporterOptions.PrepopulatedFields = new Dictionary<string, object>
                    {
                        ["cloud.role"] = "BusyWorker",
                        ["cloud.roleInstance"] = "CY1SCH030021417",
                        ["cloud.roleVer"] = "9.0.15289.2",
                    };

                    exporterOptions.TableNameMappings = new Dictionary<string, string>
                    {
                        ["Grocery.FoodCategory"] = "Food",
                        ["Grocery.OperationCategory"] = "Operation",
                    };

                    // only "food", "price", "status" are the custom fields that will be logged as separate columns
                    // All other custom fields will be logged as key-value pairs under the column "env_properties"
                    exporterOptions.CustomFields = new string[] { "food", "price", "status" };
                });

                // Export the formatted message as the body when this option is set to true.
                // If this option is set to false, the exporter logs "Hello from {food} {price}." as the body
                // If this option is set to true, the exporter logs "Hello from artichoke 3.99." as the body
                loggerOptions.IncludeFormattedMessage = true;
            });
        });

        // Logs from foodLogger gets sent to Food table.
        var foodLogger = loggerFactory.CreateLogger("Grocery.FoodCategory");

        // Logs from operationLogger gets sent to Operation table.
        var operationLogger = loggerFactory.CreateLogger("Grocery.OperationCategory");

        // "dayOfWeek" is logged as a key-value pair under the column "env_properties" instead of getting logged as a separate column.
        operationLogger.LogInformation("Store {status} on {dayOfWeek}.", "opened", DateTime.UtcNow.DayOfWeek);
        foodLogger.LogInformation("Hello from {food} {price}.", "artichoke", 3.99);
        foodLogger.LogDebug("Hello from {food} {price}.", "artichoke", 3.99); // Not collected {filter is set for Information and above}
        operationLogger.LogInformation("Store {status} on {dayOfWeek}.", "closed", DateTime.UtcNow.DayOfWeek);

        // Passes an exception to the LogError method.
        // The type of the exception and exception message
        // gets stored as Part A extension "ex" for Logs
        operationLogger.LogError(new InvalidOperationException("Oops! Food is spoiled!"), "Hello from {food} {price}.", "artichoke", 3.99);
    }
}
