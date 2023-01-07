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
using System.Runtime.Serialization;
using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Trace;

public class Program
{
    public static readonly ActivitySource Source = new("DemoSource");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(Source.Name)
            .AddGenevaTraceExporter(options =>
            {
                options.ConnectionString = "EtwSession=OpenTelemetry;UseTLD=true";
                options.CustomFields = new string[] { "foo", "bar", "keyValuePairs" };
                options.PrepopulatedFields = new Dictionary<string, object>
                {
                    ["cloud.role"] = "BusyWorker",
                    ["cloud.roleInstance"] = "CY1SCH030021417",
                    ["cloud.roleVer"] = "9.0.15289.2",
                };

                // options.TableNameMappings = new Dictionary<string, string>
                // {
                //    ["Span"] = "TLDSpan",
                // };
            })
            .Build();

        var linkedTraceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());
        var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

        var links = new[]
            {
                new ActivityLink(new ActivityContext(
                    linkedTraceId,
                    linkedSpanId,
                    ActivityTraceFlags.Recorded)),
            };

        using var activity = Source.StartActivity("SayHello");
        activity?.SetTag("httpStatusCode", 200);
        activity?.SetTag("azureResourceProvider", "Microsoft.AAD");
        activity?.SetTag("clientRequestId", "58a37988-2c05-427a-891f-5e0e1266fcc5");
        activity?.SetTag("foo", 1);
        activity?.SetTag("bar", 2);
        activity?.SetTag("baz", new int[] { 1, 2, 3 });

        using var activityWithLinks = Source.StartActivity(
                "ActivityWithLinks",
                ActivityKind.Client,
                parentContext: default,
                null,
                links,
                startTime: DateTimeOffset.Now);
        activityWithLinks?.SetTag("foo", 1);
        activityWithLinks?.SetTag("bar", 2);
    }
}
