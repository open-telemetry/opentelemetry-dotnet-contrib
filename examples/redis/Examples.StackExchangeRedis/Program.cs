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
    /*
     * Setup redis service inside local docker.
     * docker run --name opentelemetry-redis-test -d -p 6379:6379 redis
     *
     * If you face any issue with the first command, do the following ones:
     * docker exec -it opentelemetry-redis-test sh
     * redis-cli
     * set bind 0.0.0.0
     * save
     */

    public static void Main()
    {
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
                .AddSource("redis-test")
                .Build();

        ActivitySource activitySource = new ActivitySource("redis-test");

        // select a database (by default, DB = 0)
        var db = connection.GetDatabase();

        // Create a scoped activity. It will end automatically when using statement ends
        using (activitySource.StartActivity("Main"))
        {
            Console.WriteLine("About to do a busy work");
            for (var i = 0; i < 5; i++)
            {
                DoWork(db, activitySource);
            }
        }
    }

    private static void DoWork(IDatabase db, ActivitySource activitySource)
    {
        // Start another activity. If another activity was already started, it'll use that activity as the parent activity.
        // In this example, the main method already started a activity, so that'll be the parent activity, and this will be
        // a child activity.
        using Activity activity = activitySource.StartActivity("DoWork");
        try
        {
            db.StringSet("key", "value " + DateTime.Now.ToLongDateString());

            Console.WriteLine("Doing busy work");
            Thread.Sleep(1000);

            // run a command, in this case a GET
            var myVal = db.StringGet("key");

            Console.WriteLine(myVal);
        }
        catch (ArgumentOutOfRangeException e)
        {
            activity.SetStatus(Status.Error.WithDescription(e.ToString()));
        }

        // Annotate our activity to capture metadata about our operation
        var attributes = new Dictionary<string, object>
                {
                    { "use", "demo" },
                };
        ActivityTagsCollection eventTags = new ActivityTagsCollection(attributes);
        activity.AddEvent(new ActivityEvent("Invoking DoWork", default, eventTags));
    }
}
