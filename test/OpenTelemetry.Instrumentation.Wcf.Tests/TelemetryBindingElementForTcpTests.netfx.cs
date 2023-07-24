// <copyright file="TelemetryBindingElementForTcpTests.netfx.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Instrumentation.Wcf.Tests.Tools;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[Collection("WCF")]
public class TelemetryBindingElementForTcpTests : IDisposable
{
    private readonly ITestOutputHelper output;
    private readonly Uri serviceBaseUri;
    private readonly ServiceHost serviceHost;

    public TelemetryBindingElementForTcpTests(ITestOutputHelper outputHelper)
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
                    new NetTcpBinding(SecurityMode.None),
                    "/Service");
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
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(true, false, false)]
    [InlineData(false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, false, true, true, true, true)]
    [InlineData(true, false, true, true, true, true, true)]
    public async Task OutgoingRequestInstrumentationTest(
        bool instrument,
        bool filter = false,
        bool suppressDownstreamInstrumentation = true,
        bool includeVersion = false,
        bool enrich = false,
        bool enrichmentException = false,
        bool emptyOrNullAction = false)
    {
        List<Activity> stoppedActivities = new List<Activity>();

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(stoppedActivities);

        if (instrument)
        {
            builder
                .AddWcfInstrumentation(options =>
                {
                    if (enrich)
                    {
                        if (!enrichmentException)
                        {
                            options.Enrich = (activity, eventName, message) =>
                            {
                                switch (eventName)
                                {
                                    case WcfEnrichEventNames.BeforeSendRequest:
                                        activity.SetTag("client.beforesendrequest", WcfEnrichEventNames.BeforeSendRequest);
                                        break;
                                    case WcfEnrichEventNames.AfterReceiveReply:
                                        activity.SetTag("client.afterreceivereply", WcfEnrichEventNames.AfterReceiveReply);
                                        break;
                                }
                            };
                        }
                        else
                        {
                            options.Enrich = (activity, eventName, message) => throw new Exception("Error while enriching activity");
                        }
                    }

                    options.OutgoingRequestFilter = (Message m) => !filter;
                    options.SuppressDownstreamInstrumentation = suppressDownstreamInstrumentation;
                    options.SetSoapMessageVersion = includeVersion;
                })
                .AddDownstreamInstrumentation();
        }

        TracerProvider tracerProvider = builder.Build();

        var client = new ServiceClient(
            new NetTcpBinding(SecurityMode.None),
            new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));
        try
        {
            client.Endpoint.EndpointBehaviors.Add(new DownstreamInstrumentationEndpointBehavior());
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());

            if (emptyOrNullAction)
            {
                await client.ExecuteWithEmptyActionNameAsync(
                    new ServiceRequest
                    {
                        Payload = "Hello Open Telemetry!",
                    }).ConfigureAwait(false);
            }
            else
            {
                await client.ExecuteAsync(
                    new ServiceRequest
                    {
                        Payload = "Hello Open Telemetry!",
                    }).ConfigureAwait(false);
            }
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

        if (instrument)
        {
            if (!suppressDownstreamInstrumentation)
            {
                Assert.NotEmpty(stoppedActivities);
                if (filter)
                {
                    Assert.Single(stoppedActivities);
                    Assert.Equal("DownstreamInstrumentation", stoppedActivities[0].OperationName);
                }
                else
                {
                    Assert.Equal(2, stoppedActivities.Count);
                    Assert.Equal("DownstreamInstrumentation", stoppedActivities[0].OperationName);
                    Assert.Equal(WcfInstrumentationActivitySource.OutgoingRequestActivityName, stoppedActivities[1].OperationName);
                }
            }
            else
            {
                if (!filter)
                {
                    Assert.NotEmpty(stoppedActivities);
                    Assert.Single(stoppedActivities);

                    Activity activity = stoppedActivities[0];

                    if (emptyOrNullAction)
                    {
                        Assert.Equal(WcfInstrumentationActivitySource.OutgoingRequestActivityName, activity.DisplayName);
                        Assert.Equal("ExecuteWithEmptyActionName", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.RpcMethodTag).Value);
                    }
                    else
                    {
                        Assert.Equal("http://opentelemetry.io/Service/Execute", activity.DisplayName);
                        Assert.Equal("Execute", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.RpcMethodTag).Value);
                    }

                    Assert.Equal(WcfInstrumentationActivitySource.OutgoingRequestActivityName, activity.OperationName);
                    Assert.Equal(WcfInstrumentationConstants.WcfSystemValue, activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.RpcSystemTag).Value);
                    Assert.Equal("http://opentelemetry.io/Service", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.RpcServiceTag).Value);
                    Assert.Equal(this.serviceBaseUri.Host, activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.NetPeerNameTag).Value);
                    Assert.Equal(this.serviceBaseUri.Port, activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.NetPeerPortTag).Value);
                    Assert.Equal("net.tcp", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.WcfChannelSchemeTag).Value);
                    Assert.Equal("/Service", activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.WcfChannelPathTag).Value);
                    if (includeVersion)
                    {
                        var value = activity.TagObjects.FirstOrDefault(t => t.Key == WcfInstrumentationConstants.SoapMessageVersionTag).Value.ToString();
                        Assert.Matches("""Soap.* \(http.*\) Addressing.* \(http.*\)""", value);
                    }

                    if (enrich && !enrichmentException)
                    {
                        Assert.Equal(WcfEnrichEventNames.BeforeSendRequest, activity.TagObjects.Single(t => t.Key == "client.beforesendrequest").Value);
                        Assert.Equal(WcfEnrichEventNames.AfterReceiveReply, activity.TagObjects.Single(t => t.Key == "client.afterreceivereply").Value);
                    }
                }
                else
                {
                    Assert.Empty(stoppedActivities);
                }
            }
        }
        else
        {
            Assert.Empty(stoppedActivities);
        }
    }

    [Fact]
    public async void ActivitiesHaveCorrectParentTest()
    {
        var testSource = new ActivitySource("TestSource");

        var stoppedActivities = new List<Activity>();
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .AddInMemoryExporter(stoppedActivities)
            .AddWcfInstrumentation()
            .Build();

        var client = new ServiceClient(
            new NetTcpBinding(SecurityMode.None),
            new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));
        try
        {
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());

            using (var parentActivity = testSource.StartActivity("ParentActivity"))
            {
                client.ExecuteSynchronous(new ServiceRequest { Payload = "Hello Open Telemetry!" });
                client.ExecuteSynchronous(new ServiceRequest { Payload = "Hello Open Telemetry!" });
                var firstAsyncCall = client.ExecuteAsync(new ServiceRequest { Payload = "Hello Open Telemetry!" });
                await client.ExecuteAsync(new ServiceRequest { Payload = "Hello Open Telemetry!" });
                await firstAsyncCall;
            }
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
            testSource.Dispose();
            WcfInstrumentationActivitySource.Options = null;
        }

        Assert.Equal(5, stoppedActivities.Count);
        Assert.All(stoppedActivities, activity => Assert.Equal(stoppedActivities[0].TraceId, activity.TraceId));
        var parent = stoppedActivities.Single(activity => activity.Parent == null);
        Assert.All(
            stoppedActivities.Where(activity => activity != parent),
            activity => Assert.Equal(parent.SpanId, activity.ParentSpanId));
    }

    [Fact]
    public async void ErrorsAreHandledProperlyTest()
    {
        var testSource = new ActivitySource("TestSource");

        var stoppedActivities = new List<Activity>();
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .AddInMemoryExporter(stoppedActivities)
            .AddWcfInstrumentation()
            .Build();

        var client = new ServiceClient(new NetTcpBinding(SecurityMode.None), new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));
        var client2 = new ServiceClient(new NetTcpBinding(SecurityMode.None), new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));
        var clientBadUrl = new ServiceClient(new NetTcpBinding(SecurityMode.None), new EndpointAddress(new Uri("net.tcp://localhost:1/Service")));
        var clientBadUrl2 = new ServiceClient(new NetTcpBinding(SecurityMode.None), new EndpointAddress(new Uri("net.tcp://localhost:1/Service")));
        try
        {
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
            client2.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
            clientBadUrl.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
            clientBadUrl2.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());

            using (var parentActivity = testSource.StartActivity("ParentActivity"))
            {
                Assert.ThrowsAny<Exception>(() => client.ErrorSynchronous());

                // weird corner case: if an async call is made as the first hit (before the client is opened) the
                // async execution context gets lost somewhere in WCF internals, so we'll explicitly open it first
                client2.Open();
                await Assert.ThrowsAnyAsync<Exception>(client2.ErrorAsync);
                Assert.ThrowsAny<Exception>(() => clientBadUrl.ExecuteSynchronous(new ServiceRequest { Payload = "Hello Open Telemetry!" }));
                await Assert.ThrowsAnyAsync<Exception>(() => clientBadUrl2.ExecuteAsync(new ServiceRequest { Payload = "Hello Open Telemetry!" }));
            }
        }
        finally
        {
            Action<ServiceClient> closeClient = client =>
            {
                if (client.State == CommunicationState.Faulted)
                {
                    client.Abort();
                }
                else
                {
                    client.Close();
                }
            };
            closeClient(client);
            closeClient(client2);
            closeClient(clientBadUrl);
            closeClient(clientBadUrl2);

            tracerProvider.Shutdown();
            tracerProvider.Dispose();
            testSource.Dispose();
            WcfInstrumentationActivitySource.Options = null;
        }

        // this is 3 instead of 5 because the clientBadUrl calls fail during Open(), before the activity is created
        Assert.Equal(3, stoppedActivities.Count);
        Assert.All(stoppedActivities, activity => Assert.Equal(stoppedActivities[0].TraceId, activity.TraceId));
        var parent = stoppedActivities.Single(activity => activity.Parent == null);
        var children = stoppedActivities.Where(activity => activity != parent);
        Assert.All(children, activity => Assert.Equal(parent.SpanId, activity.ParentSpanId));
    }

    [Fact]
    public async void OrphanedTelemetryTimesOut()
    {
        var stoppedActivities = new List<Activity>();
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(stoppedActivities)
            .AddWcfInstrumentation()
            .Build();

        var binding = new NetTcpBinding(SecurityMode.None);
        binding.SendTimeout = TimeSpan.FromMilliseconds(1000);
        var client = new ServiceClient(binding, new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));
        try
        {
            client.Endpoint.EndpointBehaviors.Add(new DownstreamInstrumentationEndpointBehavior());
            client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());
            DownstreamInstrumentationChannel.FailNextReceive();
            await Assert.ThrowsAnyAsync<Exception>(() => client.ExecuteAsync(new ServiceRequest { Payload = "Hello Open Telemetry!" }));

            for (var i = 0; i < 50; i++)
            {
                if (stoppedActivities.Count > 0)
                {
                    break;
                }

                await Task.Delay(100);
            }

            Assert.Single(stoppedActivities);
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
    }
}
#endif
