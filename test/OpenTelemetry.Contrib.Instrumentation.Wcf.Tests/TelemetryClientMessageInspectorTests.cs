﻿// <copyright file="TelemetryClientMessageInspectorTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.Wcf.Tests
{
    [Collection("WCF")]
    public class TelemetryClientMessageInspectorTests : IDisposable
    {
        private readonly Uri serviceBaseUri;
        private readonly HttpListener listener;
        private readonly EventWaitHandle initializationHandle;

        public TelemetryClientMessageInspectorTests()
        {
            Random random = new Random();
            var retryCount = 5;
            while (retryCount > 0)
            {
                try
                {
                    this.serviceBaseUri = new Uri($"http://localhost:{random.Next(2000, 5000)}/");

                    this.listener = new HttpListener();
                    this.listener.Prefixes.Add(this.serviceBaseUri.OriginalString);
                    this.listener.Start();
                    break;
                }
                catch
                {
                    this.listener.Stop();
                    this.listener = null;
                    retryCount--;
                }
            }

            if (this.listener == null)
            {
                throw new InvalidOperationException("HttpListener could not be started.");
            }

            this.initializationHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            try
            {
                Listener();

                this.initializationHandle.WaitOne();
            }
            finally
            {
                this.initializationHandle.Dispose();
                this.initializationHandle = null;
            }

            async void Listener()
            {
                while (true)
                {
                    try
                    {
                        var ctxTask = this.listener.GetContextAsync();

                        this.initializationHandle?.Set();

                        var ctx = await ctxTask.ConfigureAwait(false);

                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentType = "text/xml; charset=utf-8";

                        using (StreamWriter writer = new StreamWriter(ctx.Response.OutputStream))
                        {
                            writer.Write(@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Body><ExecuteResponse xmlns=""http://opentelemetry.io/""><ExecuteResult xmlns:a=""http://schemas.datacontract.org/2004/07/OpenTelemetry.Contrib.Instrumentation.Wcf.Tests"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""><a:Payload>RSP: Hello Open Telemetry!</a:Payload></ExecuteResult></ExecuteResponse></s:Body></s:Envelope>");
                        }

                        ctx.Response.Close();
                    }
                    catch (Exception ex)
                    {
                        if (ex is ObjectDisposedException
                            || (ex is HttpListenerException httpEx && httpEx.ErrorCode == 995))
                        {
                            // Listener was closed before we got into GetContextAsync or
                            // Listener was closed while we were in GetContextAsync.
                            break;
                        }

                        throw;
                    }
                }
            }
        }

        public void Dispose()
        {
            this.listener?.Stop();
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(true, false, false)]
        [InlineData(false)]
        public async Task OutgoingRequestInstrumentationTest(bool instrument, bool filter = false, bool suppressDownstreamInstrumentation = true)
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
                        options.OutgoingRequestFilter = (Message m) =>
                        {
                            return !filter;
                        };
                        options.SuppressDownstreamInstrumentation = suppressDownstreamInstrumentation;
                    })
                    .AddHttpClientInstrumentation() // <- Added to test SuppressDownstreamInstrumentation.
                    .Build();
            }

            ServiceClient client = new ServiceClient(
                new BasicHttpBinding(BasicHttpSecurityMode.None),
                new EndpointAddress(new Uri(this.serviceBaseUri, "/Service")));
            try
            {
                client.Endpoint.EndpointBehaviors.Add(new TelemetryEndpointBehavior());

                var response = await client.ExecuteAsync(
                    new ServiceRequest
                    {
                        Payload = "Hello Open Telemetry!",
                    });
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

                tracerProvider?.Dispose();

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
                        Assert.Equal("OpenTelemetry.HttpWebRequest.HttpRequestOut", stoppedActivities[0].OperationName);
                    }
                    else
                    {
                        Assert.Equal(2, stoppedActivities.Count);
                        Assert.Equal("OpenTelemetry.HttpWebRequest.HttpRequestOut", stoppedActivities[0].OperationName);
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
                        Assert.Equal(WcfInstrumentationActivitySource.OutgoingRequestActivityName, activity.OperationName);
                        Assert.Equal("wcf", activity.TagObjects.FirstOrDefault(t => t.Key == "rpc.system").Value);
                        Assert.Equal("http://opentelemetry.io/Service", activity.TagObjects.FirstOrDefault(t => t.Key == "rpc.service").Value);
                        Assert.Equal("Execute", activity.TagObjects.FirstOrDefault(t => t.Key == "rpc.method").Value);
                        Assert.Equal(this.serviceBaseUri.Host, activity.TagObjects.FirstOrDefault(t => t.Key == "net.peer.name").Value);
                        Assert.Equal(this.serviceBaseUri.Port, activity.TagObjects.FirstOrDefault(t => t.Key == "net.peer.port").Value);
                        Assert.Equal("Soap11 (http://schemas.xmlsoap.org/soap/envelope/) AddressingNone (http://schemas.microsoft.com/ws/2005/05/addressing/none)", activity.TagObjects.FirstOrDefault(t => t.Key == "soap.version").Value);
                        Assert.Equal("http", activity.TagObjects.FirstOrDefault(t => t.Key == "wcf.channel.scheme").Value);
                        Assert.Equal("/Service", activity.TagObjects.FirstOrDefault(t => t.Key == "wcf.channel.path").Value);
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
    }
}
