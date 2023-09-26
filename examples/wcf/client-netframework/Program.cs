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
using System.Configuration;
using System.ServiceModel;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Wcf.Client;

internal static class Program
{
    public static async Task Main()
    {
        using var openTelemetry = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Wcf-Client"))
            .AddWcfInstrumentation()
            .AddZipkinExporter()
            .Build();

        switch (ConfigurationManager.AppSettings["Server"].ToUpperInvariant())
        {
            case "ASPNET":
                await CallService("StatusService_AspNet").ConfigureAwait(false);
                break;
            default:
                await CallService("StatusService_Http").ConfigureAwait(false);
                await CallService("StatusService_Tcp").ConfigureAwait(false);
                await CallService("StatusService_Rest").ConfigureAwait(false);
                break;
        }

        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();
    }

    private static async Task CallService(string name)
    {
        // Note: Best practice is to re-use your client/channel instances.
        // This code is not meant to illustrate best practices, only the
        // instrumentation.
        StatusServiceClient client = new StatusServiceClient(name);
        try
        {
            await client.OpenAsync().ConfigureAwait(false);

            var response = await client.PingAsync(new StatusRequest(Guid.NewGuid().ToString("N"))).ConfigureAwait(false);

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
