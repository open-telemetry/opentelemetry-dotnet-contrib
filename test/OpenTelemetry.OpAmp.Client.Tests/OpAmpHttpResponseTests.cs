// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

using System.Collections.Concurrent;
using System.Net;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpHttpResponseTests
{
    [Fact]
    public async Task HttpResponses_DispatchCapabilitiesInOrder_WhenFlagsListenerQueuesReply()
    {
        const string capability = "com.example.feature/v1";
        var capabilityAdvertisement = new ServerToAgent
        {
            Flags = (ulong)ServerToAgentFlags.ReportFullState,
            CustomCapabilities = new CustomCapabilities { Capabilities = { capability } },
        };
        var capabilityWithdrawal = new ServerToAgent
        {
            CustomCapabilities = new CustomCapabilities(),
        };

        using var firstDeclaration = new ManualResetEventSlim();
        using var handler = new ResponseHandler(capabilityAdvertisement, capabilityWithdrawal);
        using var client = new OpAmpClient(settings =>
        {
            settings.ConnectionType = ConnectionType.Http;
            settings.ServerUrl = new Uri("http://localhost/v1/opamp");
            settings.Heartbeat.IsEnabled = false;
            settings.HttpClientFactory = () => new HttpClient(handler, disposeHandler: false);
        });
        var declarationsCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedDeclarations = new ConcurrentQueue<string[]>();

        client.Subscribe(new CallbackListener<FlagsMessage>(_ =>
        {
            client.SendFullStateReport(new FullStateReport());

            // Give a later response a chance to overtake this frame's capability callback.
            firstDeclaration.Wait(TimeSpan.FromSeconds(1));
        }));
        client.Subscribe(new CallbackListener<CustomCapabilitiesMessage>(message =>
        {
            receivedDeclarations.Enqueue([.. message.Capabilities]);
            firstDeclaration.Set();
            if (receivedDeclarations.Count == 2)
            {
                declarationsCompleted.TrySetResult(true);
            }
        }));

        try
        {
            var startup = client.StartAsync(TestContext.Current.CancellationToken);
            await WaitForTaskAsync(Task.WhenAll(startup, declarationsCompleted.Task));
        }
        finally
        {
            firstDeclaration.Set();
        }

        Assert.Equal([[capability], []], receivedDeclarations.ToArray());
    }

    private static async Task WaitForTaskAsync(Task task)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var timeout = Task.Delay(TimeSpan.FromSeconds(5), cancellation.Token);
        if (await Task.WhenAny(task, timeout) != task)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("The operation did not complete in time.");
        }

        cancellation.Cancel();
        await task;
    }

    private sealed class CallbackListener<T> : IOpAmpListener<T>
        where T : OpAmpMessage
    {
        private readonly Action<T> callback;

        public CallbackListener(Action<T> callback)
        {
            this.callback = callback;
        }

        public void HandleMessage(T message) => this.callback(message);
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<ServerToAgent> responses;

        public ResponseHandler(params ServerToAgent[] responses)
        {
            this.responses = new(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!this.responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("No HTTP response is queued.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(response.ToByteArray()),
            });
        }
    }
}
