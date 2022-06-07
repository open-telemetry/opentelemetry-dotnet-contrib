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

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace GettingStartedPrometheusGrafana;

public class Program
{
    public static void Main()
    {
        // Prerequisite:
        /*
         * Setup redis service inside local docker.
         * docker run --name opentelemetry-redis-test -d -p 6379:6379 redis
         */

        // connect to the redis server. The default port 6379 will be used.
        var connection = ConnectionMultiplexer.Connect("localhost");

        // Configure exporter to export traces to Zipkin
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddConsoleExporter()
                .AddRedisInstrumentation(connection, options =>
                {
                    // changing flushinterval from 10s to 5s
                    options.FlushInterval = TimeSpan.FromSeconds(5);
                })
                .Build();

        // select a database (by default, DB = 0)
        var db = connection.GetDatabase();
        db.StringSet("key", "value " + DateTime.Now.ToLongDateString());
        Thread.Sleep(1000);

        // run a command, in this case a GET
        var myVal = db.StringGet("key");
    }
}
