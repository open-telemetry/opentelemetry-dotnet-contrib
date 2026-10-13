// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Sockets;
using System.Text;

namespace OpenTelemetry.Resources.Azure.Tests;

[Collection(nameof(AzureVmMetadataRequestTests))]
[CollectionDefinition(nameof(AzureVmMetadataRequestTests), DisableParallelization = true)]
public sealed class AzureVmMetadataRequestTests
{
    private static readonly TimeSpan GenerousWait = TimeSpan.FromSeconds(25);

    [Fact]
    public async Task ImdsBodyThatNeverArrivesIsAbandonedByTheTimeout()
    {
        using var endpoint = new FakeImdsEndpoint(FakeImdsEndpoint.ResponseMode.HeadersThenStall);
        using var scope = RouteProductionRequestorTo(endpoint);

        var build = BuildResourceOnDedicatedThread();

        try
        {
            Assert.True(
                await CompletesWithinAsync(endpoint.HeadersSent, GenerousWait),
                "The production IMDS request never reached the stand-in endpoint.");

            // The endpoint keeps the connection open without sending the rest of the body, so only the
            // request timeout can end the read.
            Assert.True(
                await CompletesWithinAsync(build, GenerousWait),
                "Build() is still blocked reading the IMDS response body after the request timeout.");
        }
        finally
        {
            endpoint.Release();
            await CompletesWithinAsync(build, GenerousWait);
        }

        var resource = await build;
        Assert.Empty(resource.Attributes);
    }

    [Fact]
    public async Task ImdsThatNeverSendsHeadersIsAbandonedByTheTimeout()
    {
        using var endpoint = new FakeImdsEndpoint(FakeImdsEndpoint.ResponseMode.StallBeforeHeaders);
        using var scope = RouteProductionRequestorTo(endpoint);

        var build = BuildResourceOnDedicatedThread();

        try
        {
            Assert.True(
                await CompletesWithinAsync(endpoint.RequestReceived, GenerousWait),
                "The production IMDS request never reached the stand-in endpoint.");

            Assert.True(
                await CompletesWithinAsync(build, GenerousWait),
                "Build() should be abandoned by the 2 s timeout when no headers arrive.");
        }
        finally
        {
            endpoint.Release();
            await CompletesWithinAsync(build, GenerousWait);
        }

        var resource = await build;
        Assert.Empty(resource.Attributes);
    }

    [Fact]
    public async Task ImdsThatAnswersPromptlyIsParsedThroughTheSameInterceptedPath()
    {
        using var endpoint = new FakeImdsEndpoint(FakeImdsEndpoint.ResponseMode.Complete);
        using var scope = RouteProductionRequestorTo(endpoint);

        var build = BuildResourceOnDedicatedThread();

        try
        {
            Assert.True(await CompletesWithinAsync(build, GenerousWait), "Build() did not complete for a prompt IMDS response.");
        }
        finally
        {
            endpoint.Release();
        }

        var resource = await build;

        Assert.Contains(new KeyValuePair<string, object>("host.name", "poc-vm"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("cloud.platform", "azure.vm"), resource.Attributes);

        var requestHead = endpoint.RequestHead;
        Assert.NotNull(requestHead);
        Assert.Contains("169.254.169.254/metadata/instance/compute?api-version=2021-12-13&format=json", requestHead, StringComparison.Ordinal);
        Assert.Contains("Metadata: True", requestHead, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FailedDetectionIsRememberedBriefly()
    {
        var calls = DetectAfterTransientFailure(failedDetectionCacheDuration: TimeSpan.FromHours(1), out var second);

        Assert.Equal(1, calls);
        Assert.Empty(second.Attributes);
    }

    [Fact]
    public void FailedDetectionIsRetriedOnceTheCacheDurationHasElapsed()
    {
        var calls = DetectAfterTransientFailure(failedDetectionCacheDuration: TimeSpan.Zero, out var second);

        Assert.Equal(2, calls);
        Assert.Contains(new KeyValuePair<string, object>("host.name", "poc-vm"), second.Attributes);
    }

    private static int DetectAfterTransientFailure(TimeSpan failedDetectionCacheDuration, out Resource second)
    {
        var originalRequestor = AzureVmMetaDataRequestor.GetAzureVmMetaDataResponse;
        var originalCacheDuration = AzureVMResourceDetector.FailedDetectionCacheDuration;
        var calls = 0;

        try
        {
            using var appServiceScope = EnvironmentVariableScope.Create(ResourceAttributeConstants.AppServiceSiteNameEnvVar, null);

            AzureVmMetaDataRequestor.GetAzureVmMetaDataResponse = () =>
            {
                return Interlocked.Increment(ref calls) == 1
                    ? throw new HttpRequestException("Transient IMDS failure.")
                    : new AzureVmMetadataResponse { Name = "poc-vm", VmId = "vm-id", Location = "eastus" };
            };

            AzureVMResourceDetector.FailedDetectionCacheDuration = failedDetectionCacheDuration;
            AzureVMResourceDetector.ClearCachedResource();

            var first = ResourceBuilder.CreateEmpty().AddAzureVMDetector().Build();
            Assert.Empty(first.Attributes);

            second = ResourceBuilder.CreateEmpty().AddAzureVMDetector().Build();
            return Volatile.Read(ref calls);
        }
        finally
        {
            AzureVmMetaDataRequestor.GetAzureVmMetaDataResponse = originalRequestor;
            AzureVMResourceDetector.FailedDetectionCacheDuration = originalCacheDuration;
            AzureVMResourceDetector.ClearCachedResource();
        }
    }

    private static Task<Resource> BuildResourceOnDedicatedThread() =>
        Task.Factory.StartNew(
            () => ResourceBuilder.CreateEmpty().AddAzureVMDetector().Build(),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    private static async Task<bool> CompletesWithinAsync(Task task, TimeSpan timeout)
    {
        var winner = await Task.WhenAny(task, Task.Delay(timeout, TestContext.Current.CancellationToken));
        return ReferenceEquals(winner, task);
    }

    private static Restorer RouteProductionRequestorTo(FakeImdsEndpoint endpoint)
    {
        var originalRequestor = AzureVmMetaDataRequestor.GetAzureVmMetaDataResponse;
        var appServiceScope = EnvironmentVariableScope.Create(ResourceAttributeConstants.AppServiceSiteNameEnvVar, null);
        var proxy = new WebProxy(new Uri($"http://127.0.0.1:{endpoint.Port}/"));

#if NET
        var originalProxy = HttpClient.DefaultProxy;
        HttpClient.DefaultProxy = proxy;
#else
        var originalProxy = WebRequest.DefaultWebProxy;
        WebRequest.DefaultWebProxy = proxy;
#endif

        // Use the real requestor (other tests replace it with stubs) and make sure nothing is served from cache.
        AzureVmMetaDataRequestor.GetAzureVmMetaDataResponse = AzureVmMetaDataRequestor.GetAzureVmMetaDataResponseDefault;
        AzureVMResourceDetector.ClearCachedResource();

        return new Restorer(() =>
        {
#if NET
            HttpClient.DefaultProxy = originalProxy;
#else
            WebRequest.DefaultWebProxy = originalProxy;
#endif
            AzureVmMetaDataRequestor.GetAzureVmMetaDataResponse = originalRequestor;
            AzureVMResourceDetector.ClearCachedResource();
            appServiceScope.Dispose();
        });
    }

    private sealed class Restorer(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    /// <summary>
    /// Minimal raw-TCP HTTP/1.1 endpoint standing in for whatever answers for 169.254.169.254 on the network path.
    /// </summary>
    private sealed class FakeImdsEndpoint : IDisposable
    {
        private const string MetadataJson = """{"name":"poc-vm","vmId":"00000000-0000-0000-0000-000000000001","location":"eastus","osType":"Linux"}""";

        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly ResponseMode mode;
        private readonly ManualResetEventSlim release = new(false);
        private readonly TaskCompletionSource<bool> requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> headersSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<TcpClient> clients = [];
        private readonly Thread acceptThread;

        public FakeImdsEndpoint(ResponseMode mode)
        {
            this.mode = mode;
            this.listener.Start();
            this.Port = ((IPEndPoint)this.listener.LocalEndpoint).Port;
            this.acceptThread = new Thread(this.AcceptLoop) { IsBackground = true, Name = "FakeImdsEndpoint" };
            this.acceptThread.Start();
        }

        public enum ResponseMode
        {
            /// <summary>Send a complete, valid IMDS response immediately.</summary>
            Complete,

            /// <summary>Read the request and never answer (stall before the headers).</summary>
            StallBeforeHeaders,

            /// <summary>Send the status line, headers and the first body byte, then stall.</summary>
            HeadersThenStall,
        }

        public int Port { get; }

        public Task RequestReceived => this.requestReceived.Task;

        public Task HeadersSent => this.headersSent.Task;

        public string? RequestHead { get; private set; }

        public void Release() => this.release.Set();

        public void Dispose()
        {
            this.release.Set();
            this.listener.Stop();

            lock (this.clients)
            {
                foreach (var client in this.clients)
                {
                    client.Close();
                }
            }

            this.acceptThread.Join(TimeSpan.FromSeconds(10));
            this.release.Dispose();
        }

        private static string ReadRequestHead(NetworkStream stream)
        {
            var head = new StringBuilder();
            var buffer = new byte[1];

            while (head.Length < 64 * 1024)
            {
                if (stream.Read(buffer, 0, 1) == 0)
                {
                    break;
                }

                head.Append((char)buffer[0]);

                if (head.Length >= 4 && head.ToString(head.Length - 4, 4) == "\r\n\r\n")
                {
                    break;
                }
            }

            return head.ToString();
        }

        private static void Write(NetworkStream stream, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private void AcceptLoop()
        {
            while (true)
            {
                TcpClient client;

                try
                {
                    client = this.listener.AcceptTcpClient();
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }

                lock (this.clients)
                {
                    this.clients.Add(client);
                }

                try
                {
                    this.Handle(client);
                }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                {
                    // The client went away or the endpoint is being disposed.
                }
                finally
                {
                    client.Close();
                }
            }
        }

        private void Handle(TcpClient client)
        {
            var stream = client.GetStream();

            this.RequestHead = ReadRequestHead(stream);
            this.requestReceived.TrySetResult(true);

            switch (this.mode)
            {
                case ResponseMode.Complete:
                    var completeResponse =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: application/json; charset=utf-8\r\n" +
                        $"Content-Length: {Encoding.UTF8.GetByteCount(MetadataJson)}\r\n" +
                        "Connection: close\r\n" +
                        "\r\n" +
                        MetadataJson;
                    Write(stream, completeResponse);
                    break;

                case ResponseMode.StallBeforeHeaders:
                    this.release.Wait();
                    break;

                case ResponseMode.HeadersThenStall:
                    // A syntactically valid response whose body never finishes arriving.
                    var stalledResponse =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: application/json; charset=utf-8\r\n" +
                        "Content-Length: 1024\r\n" +
                        "\r\n" +
                        "{";
                    Write(stream, stalledResponse);
                    this.headersSent.TrySetResult(true);
                    this.release.Wait();
                    break;

                default:
                    break;
            }
        }
    }
}
