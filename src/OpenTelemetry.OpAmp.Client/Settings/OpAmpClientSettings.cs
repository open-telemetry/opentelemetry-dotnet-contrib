// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.WebSockets;

using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal;

namespace OpenTelemetry.OpAmp.Client.Settings;

/// <summary>
/// OpAMP client settings.
/// </summary>
public sealed class OpAmpClientSettings
{
    private static readonly Func<ClientWebSocket> DefaultClientWebSocketFactory = static () => new();

    private Func<ClientWebSocket>? clientWebSocketFactory;
    private Func<HttpClient>? defaultHttpClientFactory;
    private int maxPendingCustomMessages = OpAmpClientDefaults.MaxPendingCustomMessages;
    private int maxPendingMessageBytes = OpAmpClientDefaults.MaxPendingMessageBytes;

    /// <summary>
    /// Gets or sets the unique identifier for the current client instance.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> that uniquely identifies this instance.
    /// By default:
    /// <list type="bullet">
    ///   <item>
    ///     <description>On .NET 9.0 or greater: initialized with a Version 7 GUID.</description>
    ///   </item>
    ///   <item>
    ///     <description>On earlier versions: initialized with a randomly generated GUID.</description>
    ///   </item>
    /// </list>
    /// </value>
    /// <remarks>
    /// If the server assigns a new instance UID, the client uses it for all further
    /// communication. This property keeps the configured value.
    /// </remarks>
    public Guid InstanceUid { get; set; }
#if NET
        = Guid.CreateVersion7();
#else
        = Guid.NewGuid();
#endif

    /// <summary>
    /// Gets or sets the type of connection used to communicate with the server
    /// (for example, HTTP or WebSocket).
    /// </summary>
    /// <value>
    /// A <see cref="ConnectionType"/> value that specifies the transport protocol.
    /// The default is <see cref="ConnectionType.Http"/>.
    /// </value>
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Http;

    /// <summary>
    /// Gets or sets the server URL to connect to.
    /// </summary>
    /// <value>
    /// The <see cref="Uri"/> representing the server endpoint.
    /// If not explicitly set, a default URL is returned based on the <see cref="ConnectionType"/>:
    /// <list type="bullet">
    ///   <item>
    ///     <description><see cref="ConnectionType.Http"/> -> <c>https://localhost:4320/v1/opamp</c></description>
    ///   </item>
    ///   <item>
    ///     <description><see cref="ConnectionType.WebSocket"/> -> <c>wss://localhost:4320/v1/opamp</c></description>
    ///   </item>
    /// </list>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <see cref="ConnectionType"/> is not recognized.
    /// </exception>
    public Uri ServerUrl
    {
        get => field ?? this.ConnectionType switch
        {
            ConnectionType.Http => new("https://localhost:4320/v1/opamp"),
            ConnectionType.WebSocket => new("wss://localhost:4320/v1/opamp"),
            _ => throw new InvalidOperationException("Unknown connection type"),
        };
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the default HTTP and WebSocket clients
    /// follow HTTP redirects. The default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>When <see langword="true"/>, the server can choose the destination the
    /// client connects to. Enable it only for servers you trust, over validated TLS.</item>
    /// <item>This only applies to the default clients. It has no effect on a client
    /// returned by <see cref="HttpClientFactory"/> or <see cref="ClientWebSocketFactory"/>,
    /// which own their redirect behavior.</item>
    /// <item>For WebSocket connections, redirects can only be disabled on .NET 10 or
    /// later. On .NET Framework and .NET Standard 2.0 targets running on .NET
    /// Framework, WebSocket redirects are followed regardless of this setting.</item>
    /// </list>
    /// </remarks>
    public bool AllowRedirects { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of custom messages that may wait to be sent.
    /// The default is <c>2,048</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than <c>1</c>.
    /// </exception>
    public int MaxPendingCustomMessages
    {
        get => this.maxPendingCustomMessages;
        set
        {
            Guard.ThrowIfOutOfRange(value, min: 1);
            this.maxPendingCustomMessages = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum aggregate size, in bytes, of the custom messages
    /// waiting to be sent, including any state reports sent with them. The default is
    /// 64 MiB (<c>67,108,864</c> bytes).
    /// </summary>
    /// <remarks>
    /// This is an approximate limit rather than a strict bound on memory use. It does
    /// not include the message currently being sent or protocol overhead.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than <c>1</c>.
    /// </exception>
    public int MaxPendingMessageBytes
    {
        get => this.maxPendingMessageBytes;
        set
        {
            Guard.ThrowIfOutOfRange(value, min: 1);
            this.maxPendingMessageBytes = value;
        }
    }

    /// <summary>
    /// Gets or sets the identification settings.
    /// </summary>
    public IdentificationSettings Identification { get; set; } = new();

    /// <summary>
    /// Gets or sets the heartbeat settings.
    /// </summary>
    public HeartbeatSettings Heartbeat { get; set; } = new();

    /// <summary>
    /// Gets or sets the remote configuration settings.
    /// </summary>
    public RemoteConfigSettings RemoteConfiguration { get; set; } = new();

    /// <summary>
    /// Gets or sets the effective configuration reporting settings.
    /// </summary>
    public EffectiveConfigSettings EffectiveConfigurationReporting { get; set; } = new();

    /// <summary>
    /// Gets or sets the factory function called to create the <see
    /// cref="ClientWebSocket"/> instance used by the OpAmp client for
    /// <see cref="ConnectionType.WebSocket"/> transport.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is only invoked for the <see
    /// cref="ConnectionType.WebSocket"/> protocol.</item>
    /// <item>The factory must return a new, unconnected <see
    /// cref="ClientWebSocket"/> instance each time it is invoked.</item>
    /// <item>The returned instance is owned and disposed by the OpAmp
    /// client.</item>
    /// <item>Available <see cref="ClientWebSocket.Options"/>
    /// configuration varies by target framework.</item>
    /// <item>When not explicitly set, the default client follows HTTP
    /// redirects during the WebSocket handshake only when <see
    /// cref="AllowRedirects"/> is <see langword="true"/> (on .NET 10 and later).
    /// A client returned by a custom factory owns its redirect behavior, which
    /// follows redirects by default; <see cref="AllowRedirects"/> does not
    /// apply to it.</item>
    /// </list>
    /// </remarks>
    public Func<ClientWebSocket> ClientWebSocketFactory
    {
        get => this.clientWebSocketFactory ?? DefaultClientWebSocketFactory;
        set
        {
            Guard.ThrowIfNull(value);
            this.clientWebSocketFactory = value;
        }
    }

    /// <summary>
    /// Gets or sets the factory function called to create the <see
    /// cref="HttpClient"/> instance that will be used at runtime to
    /// transmit OpAmp messages over HTTP.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is only invoked for the <see
    /// cref="ConnectionType.Http"/> protocol.</item>
    /// <item>The factory must return a new <see cref="HttpClient"/>
    /// instance each time it is invoked.</item>
    /// <item>The returned instance is owned and disposed by the OpAmp
    /// client.</item>
    /// <item>When not explicitly set, a default <see
    /// cref="HttpClient"/> instance is created that follows HTTP redirects
    /// only when <see cref="AllowRedirects"/> is <see langword="true"/>.</item>
    /// <item>A client returned by a custom factory owns its redirect
    /// behavior; <see cref="AllowRedirects"/> does not apply to it.</item>
    /// </list>
    /// </remarks>
    public Func<HttpClient> HttpClientFactory
    {
        get => field ?? (this.defaultHttpClientFactory ??= this.CreateDefaultHttpClient);
        set
        {
            Guard.ThrowIfNull(value);
            field = value;
        }
    }

    // The WebSocket transport applies AllowRedirects only when the default factory is in use.
    internal bool UsesDefaultClientWebSocketFactory => this.clientWebSocketFactory is null;

    // CA2000: HttpClient owns and disposes the handler.
    // CA5399: the handler's default certificate revocation checking is intentionally left unchanged.
#pragma warning disable CA5399, CA2000
    private HttpClient CreateDefaultHttpClient() => new(new HttpClientHandler { AllowAutoRedirect = this.AllowRedirects });
#pragma warning restore CA5399, CA2000
}
