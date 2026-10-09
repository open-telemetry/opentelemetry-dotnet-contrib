// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OpenTelemetry.Resources.AWS.Tests;

[Collection(nameof(MetadataEndpointTimeoutTests))]
[CollectionDefinition(nameof(MetadataEndpointTimeoutTests), DisableParallelization = true)]
public sealed class MetadataEndpointTimeoutTests
{
    private static readonly TimeSpan MaxBuildTime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GenerousWait = TimeSpan.FromSeconds(25);

    [Fact]
    public async Task Ec2DetectorReadsPromptImdsResponses()
    {
        using var endpoint = new FakeMetadataEndpoint(FakeMetadataEndpoint.ResponseMode.Complete);
        using var proxy = RouteDefaultProxyTo(endpoint);

        var build = BuildOnDedicatedThread(() => ResourceBuilder.CreateEmpty().AddAWSEC2Detector().Build());

        try
        {
            Assert.True(await CompletesWithinAsync(build, GenerousWait), "Build() did not complete for a prompt IMDS.");
        }
        finally
        {
            endpoint.Release();
        }

        var attributes = (await build).Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("i-0123456789abcdef0", attributes["host.id"]);
        Assert.Equal("ip-10-0-0-1.ec2.internal", attributes["host.name"]);

        var requests = endpoint.RequestHeads.ToArray();
        Assert.Equal(3, requests.Length);
        Assert.StartsWith("PUT http://169.254.169.254/latest/api/token ", requests[0], StringComparison.Ordinal);
        Assert.StartsWith("GET http://169.254.169.254/latest/dynamic/instance-identity/document ", requests[1], StringComparison.Ordinal);
        Assert.Contains("X-aws-ec2-metadata-token: test-token", requests[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ec2DetectorGivesUpOnImdsResponseBodyThatStalls()
    {
        using var endpoint = new FakeMetadataEndpoint(FakeMetadataEndpoint.ResponseMode.HeadersThenStall);
        using var proxy = RouteDefaultProxyTo(endpoint);

        var stopwatch = Stopwatch.StartNew();
        var build = BuildOnDedicatedThread(() => ResourceBuilder.CreateEmpty().AddAWSEC2Detector().Build());

        try
        {
            Assert.True(await CompletesWithinAsync(endpoint.HeadersSent, GenerousWait), "The IMDSv2 token request never reached the stand-in endpoint.");

            Assert.True(
                await CompletesWithinAsync(build, MaxBuildTime),
                $"Build() was still blocked after {stopwatch.Elapsed.TotalSeconds:F1}s.");
        }
        finally
        {
            endpoint.Release();
        }

        Assert.Empty((await build).Attributes);
    }

    [Fact]
    public async Task Ec2DetectorGivesUpOnImdsThatDoesNotAnswer()
    {
        using var endpoint = new FakeMetadataEndpoint(FakeMetadataEndpoint.ResponseMode.StallBeforeHeaders);
        using var proxy = RouteDefaultProxyTo(endpoint);

        var stopwatch = Stopwatch.StartNew();
        var build = BuildOnDedicatedThread(() => ResourceBuilder.CreateEmpty().AddAWSEC2Detector().Build());

        try
        {
            Assert.True(await CompletesWithinAsync(endpoint.RequestReceived, GenerousWait), "The IMDSv2 token request never reached the stand-in endpoint.");

            Assert.True(
                await CompletesWithinAsync(build, MaxBuildTime),
                $"Build() was still blocked after {stopwatch.Elapsed.TotalSeconds:F1}s.");
        }
        finally
        {
            endpoint.Release();
        }

        Assert.Empty((await build).Attributes);
    }

#if NET
    [Fact]
    public async Task EcsDetectorGivesUpOnTaskMetadataResponseBodyThatStalls()
    {
        using var endpoint = new FakeMetadataEndpoint(FakeMetadataEndpoint.ResponseMode.HeadersThenStall);
        using var proxy = RouteDefaultProxyTo(endpoint);

        using var environment = EnvironmentVariableScope.Create("ECS_CONTAINER_METADATA_URI_V4", "http://169.254.170.2/v4/5fb8fcdab2e54a6d8e3e2b5e3d8f3b5a-1234567890");

        var stopwatch = Stopwatch.StartNew();
        var build = BuildOnDedicatedThread(() => ResourceBuilder.CreateEmpty().AddAWSECSDetector().Build());

        try
        {
            Assert.True(await CompletesWithinAsync(endpoint.HeadersSent, GenerousWait), "The ECS task metadata request never reached the stand-in endpoint.");

            Assert.True(
                await CompletesWithinAsync(build, MaxBuildTime),
                $"Build() was still blocked after {stopwatch.Elapsed.TotalSeconds:F1}s.");
        }
        finally
        {
            endpoint.Release();
        }

        var attributes = (await build).Attributes.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("aws_ecs", attributes["cloud.platform"]);
        Assert.DoesNotContain("aws.ecs.task.arn", attributes.Keys);

        var requests = endpoint.RequestHeads.ToArray();
        Assert.StartsWith("GET http://169.254.170.2/v4/", requests[0], StringComparison.Ordinal);
    }
#endif

    private static Task<Resource> BuildOnDedicatedThread(Func<Resource> build) =>
        Task.Factory.StartNew(build, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static async Task<bool> CompletesWithinAsync(Task task, TimeSpan timeout)
    {
        var winner = await Task.WhenAny(task, Task.Delay(timeout, TestContext.Current.CancellationToken));
        return ReferenceEquals(winner, task);
    }

    private static Restorer RouteDefaultProxyTo(FakeMetadataEndpoint endpoint)
    {
        var proxy = new WebProxy(new Uri($"http://127.0.0.1:{endpoint.Port}/"));

#if NET
        var originalProxy = HttpClient.DefaultProxy;
        HttpClient.DefaultProxy = proxy;
        return new Restorer(() => HttpClient.DefaultProxy = originalProxy);
#else
        var originalProxy = WebRequest.DefaultWebProxy;
        WebRequest.DefaultWebProxy = proxy;
        return new Restorer(() => WebRequest.DefaultWebProxy = originalProxy);
#endif
    }

    private sealed class Restorer(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    /// <summary>
    /// Minimal raw-TCP HTTP/1.1 endpoint (used as an HTTP proxy) standing in for whatever
    /// answers for the AWS metadata addresses on the network path.
    /// </summary>
    private sealed class FakeMetadataEndpoint : IDisposable
    {
        private const string IdentityDocumentJson = """{"accountId":"123456789012","availabilityZone":"us-east-1a","region":"us-east-1","instanceId":"i-0123456789abcdef0","instanceType":"t3.micro","imageId":"ami-0123456789abcdef0"}""";

        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly ResponseMode mode;
        private readonly ManualResetEventSlim release = new(false);
        private readonly TaskCompletionSource<bool> requestReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> headersSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<TcpClient> clients = [];
        private readonly Thread acceptThread;

        public FakeMetadataEndpoint(ResponseMode mode)
        {
            this.mode = mode;
            this.listener.Start();
            this.Port = ((IPEndPoint)this.listener.LocalEndpoint).Port;
            this.acceptThread = new Thread(this.AcceptLoop) { IsBackground = true, Name = nameof(FakeMetadataEndpoint) };
            this.acceptThread.Start();
        }

        public enum ResponseMode
        {
            /// <summary>Answer every request promptly with a valid EC2 IMDSv2 response.</summary>
            Complete,

            /// <summary>Read the request and never answer (stall before the headers).</summary>
            StallBeforeHeaders,

            /// <summary>Send the status line, headers and the first body byte, then stall.</summary>
            HeadersThenStall,
        }

        public int Port { get; }

        public Task RequestReceived => this.requestReceived.Task;

        public Task HeadersSent => this.headersSent.Task;

        public ConcurrentQueue<string> RequestHeads { get; } = new();

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

        private static string CompleteResponse(string body) =>
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/plain\r\n" +
            $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
            "Connection: close\r\n" +
            "\r\n" +
            body;

        private void AcceptLoop()
        {
            // More connections than any test makes, so that the loop cannot run forever.
            for (var connections = 0; connections < 1_000; connections++)
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

            var head = ReadRequestHead(stream);
            this.RequestHeads.Enqueue(head);
            this.requestReceived.TrySetResult(true);

            switch (this.mode)
            {
                case ResponseMode.Complete:
                    var body =
                        head.Contains("/latest/api/token") ? "test-token" :
                        head.Contains("/latest/dynamic/instance-identity/document") ? IdentityDocumentJson :
                        head.Contains("/latest/meta-data/hostname") ? "ip-10-0-0-1.ec2.internal" :
                        string.Empty;
                    Write(stream, CompleteResponse(body));
                    break;

                case ResponseMode.StallBeforeHeaders:
                    this.release.Wait();
                    break;

                case ResponseMode.HeadersThenStall:
                    // A syntactically valid response whose body never finishes arriving.
                    Write(stream, "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 1024\r\n\r\n{");
                    this.headersSent.TrySetResult(true);
                    this.release.Wait();
                    break;

                default:
                    break;
            }
        }
    }
}
