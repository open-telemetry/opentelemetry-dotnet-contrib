// <copyright file="TelemetryDispatchMessageInspectorTests.netfx.cs" company="OpenTelemetry Authors">
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
#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.Wcf.Tests
{
    [Collection("WCF")]
    public class TelemetryDispatchMessageInspectorTests : IDisposable
    {
        private readonly Uri serviceBaseUri;
        private readonly ServiceHost serviceHost;

        public TelemetryDispatchMessageInspectorTests()
        {
            Random random = new Random();
            var retryCount = 5;
            while (retryCount > 0)
            {
                try
                {
                    this.serviceBaseUri = new Uri($"http://localhost:{random.Next(2000, 5000)}/");
                    this.serviceHost = new ServiceHost(new Service(), this.serviceBaseUri);
                    var endpoint = this.serviceHost.AddServiceEndpoint(
                        typeof(IServiceContract),
                        new BasicHttpBinding(BasicHttpSecurityMode.None),
                        "/Service");
                    endpoint.Behaviors.Add(new TelemetryEndpointBehavior());
                    this.serviceHost.Open();
                    break;
                }
                catch
                {
                    this.serviceHost.Close();
                    this.serviceHost = null;
                    retryCount--;
                }
            }

            if (this.serviceHost == null)
            {
                throw new InvalidOperationException("ServiceHost could not be started.");
            }
        }

        public void Dispose()
        {
            this.serviceHost?.Close();
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false)]
        public async Task IncomingRequestInstrumentationTest(bool instrument, bool filter = false)
        {
            List<Activity> stoppedActivities = new List<Activity>();

            using ActivityListener activityListener = new ActivityListener
            {
                ShouldListenTo = activitySource => true,
                ActivityStopped = activity => stoppedActivities.Add(activity),
            };

            ActivitySource.AddActivityListener(activityListener);

            TracerProvider tracerProvider = null;
            if (instrument)
            {
                tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddWcfInstrumentation(options =>
                    {
                        options.IncomingRequestFilter = (Message m) =>
                        {
                            return !filter;
                        };
                    })
                    .Build();
            }

            ServiceClient client = new ServiceClient(
                new BasicHttpBinding(BasicHttpSecurityMode.None),
                new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));
            try
            {
                var response = await client.ExecuteAsync(
                    new ServiceRequest
                    {
                        Payload = "Hello Open Telemetry!",
                    }).ConfigureAwait(false);
            }
            finally
            {
                if (client.State == CommunicationState.Faulted)
                {
                    client.Abort();
                }
                else
                {
                    client.Close();
                }

                tracerProvider?.Shutdown();
                tracerProvider?.Dispose();

                WcfInstrumentationActivitySource.Options = null;
            }

            if (instrument && !filter)
            {
                Assert.NotEmpty(stoppedActivities);
                Assert.Single(stoppedActivities);

                Activity activity = stoppedActivities[0];
                Assert.Equal(WcfInstrumentationActivitySource.IncomingRequestActivityName, activity.OperationName);
                Assert.Equal("wcf", activity.TagObjects.FirstOrDefault(t => t.Key == "rpc.system").Value);
                Assert.Equal("http://opentelemetry.io/Service", activity.TagObjects.FirstOrDefault(t => t.Key == "rpc.service").Value);
                Assert.Equal("Execute", activity.TagObjects.FirstOrDefault(t => t.Key == "rpc.method").Value);
                Assert.Equal(this.serviceBaseUri.Host, activity.TagObjects.FirstOrDefault(t => t.Key == "net.host.name").Value);
                Assert.Equal(this.serviceBaseUri.Port, activity.TagObjects.FirstOrDefault(t => t.Key == "net.host.port").Value);
                Assert.Equal("Soap11 (http://schemas.xmlsoap.org/soap/envelope/) AddressingNone (http://schemas.microsoft.com/ws/2005/05/addressing/none)", activity.TagObjects.FirstOrDefault(t => t.Key == "soap.version").Value);
                Assert.Equal("http", activity.TagObjects.FirstOrDefault(t => t.Key == "wcf.channel.scheme").Value);
                Assert.Equal("/Service", activity.TagObjects.FirstOrDefault(t => t.Key == "wcf.channel.path").Value);
                Assert.Equal("http://opentelemetry.io/Service/ExecuteResponse", activity.TagObjects.FirstOrDefault(t => t.Key == "soap.reply_action").Value);
            }
            else
            {
                Assert.Empty(stoppedActivities);
            }
        }
    }
}
#endif
