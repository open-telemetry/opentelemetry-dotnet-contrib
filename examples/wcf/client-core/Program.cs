﻿// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Contrib.Instrumentation.Wcf;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Wcf.Client
{
    internal static class Program
    {
        public static async Task Main()
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Wcf-Client-Core"))
                .AddWcfInstrumentation()
                .AddZipkinExporter()
                .Build();

            await CallService(
                new BasicHttpBinding(BasicHttpSecurityMode.None),
                new EndpointAddress(config.GetSection("Service").GetValue<string>("HttpAddress"))).ConfigureAwait(false);
            await CallService(
                new NetTcpBinding(SecurityMode.None),
                new EndpointAddress(config.GetSection("Service").GetValue<string>("TcpAddress"))).ConfigureAwait(false);

            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();
        }

        private static async Task CallService(Binding binding, EndpointAddress remoteAddress)
        {
            // Note: Best practice is to re-use your client/channel instances.
            // This code is not meant to illustrate best practices, only the
            // instrumentation.
            StatusServiceClient client = new StatusServiceClient(binding, remoteAddress);
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
            try
            {
                await client.OpenAsync().ConfigureAwait(false);

                var response = await client.PingAsync(
                    new StatusRequest
                    {
                        Status = Guid.NewGuid().ToString("N"),
                    }).ConfigureAwait(false);

                Console.WriteLine($"Server returned: {response?.ServerTime}");
            }
            finally
            {
                try
                {
                    if (client.State == CommunicationState.Faulted)
                    {
                        client.Abort();
                    }
                    else
                    {
                        await client.CloseAsync().ConfigureAwait(false);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
