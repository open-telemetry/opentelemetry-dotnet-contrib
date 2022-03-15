// <copyright file="TelemetryDispatchMessageInspectorForOneWayOperationsTests.netfx.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Contrib.Instrumentation.Wcf.Tests.Tools;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Contrib.Instrumentation.Wcf.Tests
{
    [Collection("WCF")]
    public class TelemetryDispatchMessageInspectorForOneWayOperationsTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        private readonly Uri serviceBaseUri;
        private readonly ServiceHost serviceHost;

        private readonly EventWaitHandle thrownExceptionsHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private readonly List<Exception> thrownExceptions = new List<Exception>();

        public TelemetryDispatchMessageInspectorForOneWayOperationsTests(ITestOutputHelper outputHelper)
        {
            this.output = outputHelper;

            Random random = new Random();
            var retryCount = 5;
            while (retryCount > 0)
            {
                try
                {
                    this.serviceBaseUri = new Uri($"net.tcp://localhost:{random.Next(2000, 5000)}/");
                    this.serviceHost = new ServiceHost(new Service(), this.serviceBaseUri);

                    var endpoint = this.serviceHost.AddServiceEndpoint(
                        typeof(IServiceContract),
                        new NetTcpBinding(),
                        "/Service");
                    endpoint.Behaviors.Add(new TelemetryEndpointBehavior());

                    this.serviceHost.Description.Behaviors.Add(
                        new ErrorHandlerServiceBehavior(this.thrownExceptionsHandle, ex => this.thrownExceptions.Add(ex)));

                    this.serviceHost.Open();

                    break;
                }
                catch (Exception ex)
                {
                    this.output.WriteLine(ex.ToString());
                    if (this.serviceHost.State == CommunicationState.Faulted)
                    {
                        this.serviceHost.Abort();
                    }
                    else
                    {
                        this.serviceHost.Close();
                    }

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
            this.thrownExceptionsHandle?.Dispose();
        }

        [Fact]
        public void IncomingRequestOneWayOperationInstrumentationTest()
        {
            List<Activity> stoppedActivities = new List<Activity>();

            using ActivityListener activityListener = new ActivityListener
            {
                ShouldListenTo = activitySource => true,
                ActivityStopped = activity => stoppedActivities.Add(activity),
            };

            ActivitySource.AddActivityListener(activityListener);
            TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddWcfInstrumentation()
                    .Build();

            ServiceClient client = new ServiceClient(
               new NetTcpBinding(),
               new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));

            try
            {
                client.ExecuteWithOneWay(new ServiceRequest());
                this.thrownExceptionsHandle.WaitOne(3000);
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

            // Assert
            Assert.Empty(this.thrownExceptions);

            Activity activity = stoppedActivities[0];
            Assert.Equal("http://opentelemetry.io/Service/ExecuteWithOneWay", activity.DisplayName);
            Assert.Equal("ExecuteWithOneWay", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.RpcMethodTag).Value);
            Assert.DoesNotContain(activity.TagObjects, t => t.Key == WcfInstrumentationConstants.SoapReplyActionTag);
        }
    }
}
#endif
