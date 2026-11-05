// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

#if NET
using System.Net.WebSockets;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;
#endif

using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class RedirectTests
{
    [Fact]
    public void AllowRedirects_DefaultsToFalse()
        => Assert.False(new OpAmpClientSettings().AllowRedirects);

    [Theory]
    [InlineData(false, false, false)] // default client blocks redirects
    [InlineData(true, false, true)] // default client follows redirects when allowed
    [InlineData(false, true, true)] // custom factory owns redirect behavior
    public async Task Http_Redirect(bool allowRedirects, bool customFactory, bool expectFollowed)
    {
        var targetHits = 0;
        using var target = TestHttpServer.RunServer(
            ctx =>
            {
                Interlocked.Increment(ref targetHits);
                ctx.Response.Close();
            },
            out var targetUri);
        using var redirector = RunRedirector(targetUri, out var redirectorUri);

        var settings = new OpAmpClientSettings { ServerUrl = redirectorUri, AllowRedirects = allowRedirects };
        if (customFactory)
        {
            settings.HttpClientFactory = () => new HttpClient();
        }

        using var transport = new PlainHttpTransport(settings, new FrameProcessor());

        // The target replies with an empty body, so only whether it was contacted matters.
        var ex = await Record.ExceptionAsync(
            () => transport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None));

        if (expectFollowed)
        {
            Assert.IsNotType<HttpRequestException>(ex);
        }
        else
        {
            Assert.IsType<HttpRequestException>(ex);
        }

        Assert.Equal(expectFollowed ? 1 : 0, Volatile.Read(ref targetHits));
    }

#if NET
    [Theory]
    [InlineData(false, false, false)] // default client blocks redirects
    [InlineData(true, false, true)] // default client follows redirects when allowed
    [InlineData(false, true, true)] // custom factory owns redirect behavior
    public async Task WebSocket_Redirect(bool allowRedirects, bool customFactory, bool expectFollowed)
    {
        using var targetReached = new ManualResetEventSlim();
        using var target = TestWebSocketServer.RunServer(
            (WebSocket _) =>
            {
                targetReached.Set();
                return Task.CompletedTask;
            },
            out var targetHost,
            out var targetPort);
        using var redirector = RunRedirector(new Uri($"http://{targetHost}:{targetPort}/"), out var redirectorUri);

        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = new UriBuilder(redirectorUri) { Scheme = "ws" }.Uri,
            AllowRedirects = allowRedirects,
        };
        if (customFactory)
        {
            settings.ClientWebSocketFactory = () => new ClientWebSocket();
        }

        using var transport = new WsTransport(settings, new FrameProcessor());

        if (expectFollowed)
        {
            await transport.StartAsync(TestContext.Current.CancellationToken);
            Assert.True(targetReached.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken), "Target server was not reached.");
        }
        else
        {
            // The handshake fails on the redirect response, so the target is never contacted.
            await Assert.ThrowsAsync<WebSocketException>(() => transport.StartAsync(TestContext.Current.CancellationToken));
            Assert.False(targetReached.IsSet);
        }
    }
#endif

    private static IDisposable RunRedirector(Uri location, out Uri address)
        => TestHttpServer.RunServer(
            ctx =>
            {
                ctx.Response.StatusCode = 307;
                ctx.Response.RedirectLocation = location.ToString();
                ctx.Response.Close();
            },
            out address);
}
