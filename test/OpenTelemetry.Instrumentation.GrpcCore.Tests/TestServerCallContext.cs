// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;

namespace OpenTelemetry.Instrumentation.GrpcCore.Tests;

/// <summary>
/// A minimal <see cref="ServerCallContext"/> for invoking the server
/// interceptor directly, without a server.
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    private readonly Metadata requestHeaders;
    private readonly string host;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestServerCallContext"/> class.
    /// </summary>
    /// <param name="requestHeaders">The request headers.</param>
    /// <param name="host">The host the client used to reach the server.</param>
    public TestServerCallContext(Metadata requestHeaders, string host = "localhost")
    {
        this.requestHeaders = requestHeaders;
        this.host = host;
    }

    protected override string MethodCore => "/OpenTelemetry.Instrumentation.GrpcCore.Tests.Foobar/" + FoobarService.UnaryMethod;

    protected override string HostCore => this.host;

    protected override string PeerCore => "ipv4:127.0.0.1:12345";

    protected override DateTime DeadlineCore => DateTime.MaxValue;

    protected override Metadata RequestHeadersCore => this.requestHeaders;

    protected override CancellationToken CancellationTokenCore => CancellationToken.None;

    protected override Metadata ResponseTrailersCore { get; } = [];

    protected override Status StatusCore { get; set; }

    protected override WriteOptions? WriteOptionsCore { get; set; }

    protected override AuthContext AuthContextCore { get; } = new(null, new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;
}
