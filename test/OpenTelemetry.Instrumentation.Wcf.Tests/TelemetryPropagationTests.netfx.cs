// <copyright file="TelemetryPropagationTests.netfx.cs" company="OpenTelemetry Authors">
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
using System.ServiceModel.Description;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[Collection("WCF")]
public class TelemetryPropagationTests : IDisposable
{
    private readonly Uri serviceBaseUriTcp;
    private readonly Uri serviceBaseUriHttp;
    private readonly ServiceHost serviceHost;

    public TelemetryPropagationTests()
    {
        Random random = new Random();
        var retryCount = 5;
        while (retryCount > 0)
        {
            try
            {
                this.serviceBaseUriTcp = new Uri($"net.tcp://localhost:{random.Next(2000, 5000)}/");
                this.serviceBaseUriHttp = new Uri($"http://localhost:{random.Next(2000, 5000)}/");
                this.serviceHost = new ServiceHost(new Service(), this.serviceBaseUriTcp, this.serviceBaseUriHttp);
                var tcpEndpoint = this.serviceHost.AddServiceEndpoint(typeof(IServiceContract), new NetTcpBinding(), "/tcp");
                tcpEndpoint.Behaviors.Add(new TelemetryEndpointBehavior());
                var httpEndpoint = this.serviceHost.AddServiceEndpoint(typeof(IServiceContract), new BasicHttpBinding(), "/http");
                httpEndpoint.Behaviors.Add(new TelemetryEndpointBehavior());
                var restEndpoint = this.serviceHost.AddServiceEndpoint(typeof(IServiceContract), new WebHttpBinding(), "/rest");
                restEndpoint.Behaviors.Add(new TelemetryEndpointBehavior());
                restEndpoint.Behaviors.Add(new WebHttpBehavior());
                this.serviceHost.Open();
                break;
            }
            catch (Exception)
            {
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
    }

    [Theory]
    [InlineData("tcp")]
    [InlineData("http")]
    [InlineData("rest")]
    [InlineData("http", false, true)]
    [InlineData("tcp", false, true)]
    [InlineData("rest", false, false)]
    public async Task TelemetryContextPropagatesTest(
        string endpoint,
        bool suppressDownstreamInstrumenation = true,
        bool shouldPropagate = true)
    {
        var stoppedActivities = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = activitySource => true,
            ActivityStopped = stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(activityListener);

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddWcfInstrumentation(options => options.SuppressDownstreamInstrumentation = suppressDownstreamInstrumenation)
            .Build();

        var serviceBase = endpoint == "tcp" ? this.serviceBaseUriTcp : this.serviceBaseUriHttp;
        Binding binding = endpoint switch
        {
            "tcp" => new NetTcpBinding(),
            "http" => new BasicHttpBinding(),
            "rest" => new WebHttpBinding(),
            _ => throw new ArgumentException("Invalid endpoint type", nameof(endpoint)),
        };
        ServiceClient client = new ServiceClient(binding, new EndpointAddress(new Uri(serviceBase, $"/{endpoint}")));
        try
        {
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
            if (endpoint == "rest")
            {
                client.Endpoint.EndpointBehaviors.Add(new WebHttpBehavior());
            }

            await client.ExecuteAsync(new ServiceRequest { Payload = "Hello Open Telemetry!" });
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

            tracerProvider.Shutdown();
            tracerProvider.Dispose();

            WcfInstrumentationActivitySource.Options = null;
        }

        Assert.Equal(2, stoppedActivities.Count);
        if (shouldPropagate)
        {
            var clientSpan = stoppedActivities.Single(activity => activity.ParentId == null);
            var serverSpan = stoppedActivities.Single(activity => activity.ParentId == clientSpan.Id);
            Assert.Equal(clientSpan.TraceId, serverSpan.TraceId);
        }
        else
        {
            Assert.All(stoppedActivities, activity => Assert.Null(activity.ParentId));
            Assert.NotEqual(stoppedActivities[0].TraceId, stoppedActivities[1].TraceId);
        }
    }
}
#endif
