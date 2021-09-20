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
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Owin;

namespace Examples.Owin
{
    internal static class Program
    {
        public static void Main()
        {
            using var host = WebApp.Start(
                "http://localhost:9000",
                appBuilder =>
                {
                    appBuilder.UseOpenTelemetry();

                    HttpConfiguration config = new HttpConfiguration();

                    config.MessageHandlers.Add(new ActivityDisplayNameRouteEnrichingHandler());

                    config.Routes.MapHttpRoute(
                        name: "DefaultApi",
                        routeTemplate: "api/{controller}/{id}",
                        defaults: new { id = RouteParameter.Optional });

                    appBuilder.UseWebApi(config);
                });

            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Owin-Example"))
                .AddOwinInstrumentation()
                .AddConsoleExporter()
                .Build();

            Console.WriteLine("Service listening. Press enter to exit.");
            Console.ReadLine();
        }

        private class ActivityDisplayNameRouteEnrichingHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                try
                {
                    return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    var activity = Activity.Current;
                    if (activity != null)
                    {
                        var routeData = request.GetRouteData();
                        if (routeData != null)
                        {
                            activity.DisplayName = routeData.Route.RouteTemplate;
                        }
                    }
                }
            }
        }
    }
}
