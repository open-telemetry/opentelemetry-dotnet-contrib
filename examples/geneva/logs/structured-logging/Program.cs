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

public class Program
{
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
        .AddOpenTelemetry(loggerOptions =>
        {
            // Enable/Disable this based on your use-case.
            // loggerOptions.IncludeFormattedMessage = true;
            loggerOptions.AddGenevaLogExporter(exporterOptions =>
            {
                exporterOptions.ConnectionString = "EtwSession=OpenTelemetry";

                // On Linux
                // options.ConnectionString = "Endpoint=unix:/var/run/mdsd/default_fluent.socket";

                exporterOptions.TableNameMappings = new Dictionary<string, string>
                {
                    ["Example3-MyLog"] = "MyLog",
                    ["Example4-MyLog"] = "MyLog",
                };

                exporterOptions.CustomFields = new string[] { "food", "price" };
            });
        }));

        // Example 1
        var logger1 = loggerFactory.CreateLogger("Example1");
        logger1.Log(
            logLevel: LogLevel.Information,
            eventId: default,
            state: new List<KeyValuePair<string, object>>()
            {
                new KeyValuePair<string, object>("food", "apple"),
                new KeyValuePair<string, object>("price", 3.99),
            },
            exception: null,
            formatter: (state, ex) => "Example formatted message.");

        // Example2
        var logger2 = loggerFactory.CreateLogger("Example2");
        logger2.LogInformation("Hello from {food} {price}.", "apple", 3.99);

        // Example3
        var logger3 = loggerFactory.CreateLogger("Example3-MyLog");
        logger3.LogInformation("Hello from {food} {price}.", "apple", 3.99);

        // Example4
        var logger4 = loggerFactory.CreateLogger("Example4-MyLog");
        var myLog = new MyLog(logger4);
        myLog.SayHello("apple", 3.99);

        // Example6
        var logger6 = loggerFactory.CreateLogger("Example6");
        logger6.LogInformation("Hello from {food} {price} {color} {weight}.", "apple", 3.99, "red", 1.99);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
public partial class MyLog
#pragma warning restore SA1402 // File may only contain a single type
{
    private readonly ILogger logger;

    public MyLog(ILogger logger)
    {
        this.logger = logger;
    }

    [LoggerMessage(
        EventId = default,
        Level = LogLevel.Information,
        Message = "Hello from {food} {price}.")]
    public partial void SayHello(string food, double price, Exception ex = null);
}
