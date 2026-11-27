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
    private readonly string? method;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestServerCallContext"/> class.
    /// </summary>
    /// <param name="requestHeaders">The request headers.</param>
    /// <param name="method">The method name.</param>
    public TestServerCallContext(Metadata requestHeaders, string? method = "/OpenTelemetry.Instrumentation.GrpcCore.Tests.Foobar/" + FoobarService.UnaryMethod)
    {
        this.requestHeaders = requestHeaders;
        this.method = method;
    }

    protected override string MethodCore => this.method!;

    protected override string HostCore => "localhost";

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
