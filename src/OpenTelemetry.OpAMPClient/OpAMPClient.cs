// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Opamp.Protocol;
using OpenTelemetry.OpAMPClient.Settings;
using OpenTelemetry.OpAMPClient.Transport;
using OpenTelemetry.OpAMPClient.Trash;

namespace OpenTelemetry.OpAMPClient;

/// <summary>
/// OpAMP client implementation that connects to an OpAMP server.
/// </summary>
public class OpAMPClient
{
    private readonly ByteString instanceUid = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());
    private readonly FrameProcessor processor = new(new SampleMessageListener());
    private readonly IOpAMPTransport transport;
    private CancellationToken token;
    private ulong sequenceNum;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpAMPClient"/> class.
    /// </summary>
    /// <param name="configure">Configure OpAmp settings</param>
    public OpAMPClient(Action<OpAMPSettings>? configure = null)
    {
        var settings = new OpAMPSettings();
        configure?.Invoke(settings);

        this.transport = ConstructTransport(settings.ConnectionType, this.processor);
    }

    /// <summary>
    /// Starts the asynchronous operation to initialize the transport and send identification data.
    /// </summary>
    /// <param name="token">A <see cref="CancellationToken"/> that can be used to cancel the operation. Defaults to <see
    /// langword="default"/> if not provided.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken token = default)
    {
        this.token = token;

        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StartAsync(token).ConfigureAwait(false);
        }

        await this.SendIdentificationAsync().ConfigureAwait(false);
    }

    private static IOpAMPTransport ConstructTransport(ConnectionType connectionType, FrameProcessor processor)
    {
        return connectionType switch
        {
            ConnectionType.WebSocket => new WsTransport(processor),
            ConnectionType.Http => new HttpTransport(processor),
            _ => throw new NotSupportedException("Unsupported transport type"),
        };
    }

    private async Task SendIdentificationAsync()
    {
        var message = new AgentToServer()
        {
            InstanceUid = this.instanceUid,
            SequenceNum = this.IncrementSequenceNum(),
        };

        message.AgentDescription = new AgentDescription();
        message.AgentDescription.IdentifyingAttributes.Add(new KeyValue() { Key = "service.name", Value = new AnyValue() { StringValue = "opamp-exampleapp" } });
        message.AgentDescription.IdentifyingAttributes.Add(new KeyValue() { Key = "service.version", Value = new AnyValue() { StringValue = "1.0.0" } });
        message.AgentDescription.IdentifyingAttributes.Add(new KeyValue() { Key = "service.instance.id", Value = new AnyValue() { StringValue = Guid.NewGuid().ToString() } });

        message.Capabilities = (ulong)Enum.GetValues<AgentCapabilities>()
            .Aggregate((f1, f2) => f1 | f2);

        message.Health = new ComponentHealth()
        {
            Healthy = true,
            StartTimeUnixNano = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000, // Convert to nanoseconds
            LastError = "Unkown",
            Status = "OK",
            StatusTimeUnixNano = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000, // Convert to nanoseconds
            ComponentHealthMap =
            {
                {
                    "opamp-exampleapp", new ComponentHealth()
                    {
                        Healthy = true,
                        StartTimeUnixNano = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000, // Convert to nanoseconds
                        LastError = "Unkown",
                        Status = "OK",
                        StatusTimeUnixNano = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000, // Convert to nanoseconds
                    }
                },
            },
        };

        message.EffectiveConfig = new EffectiveConfig()
        {
            ConfigMap = new AgentConfigMap()
            {
                ConfigMap =
                {
                    { "otel.yml", new AgentConfigFile() { Body = ByteString.CopyFromUtf8(TestConfigData.Yaml), ContentType = "application/yaml" } },
                    { "otel.json", new AgentConfigFile() { Body = ByteString.CopyFromUtf8(TestConfigData.Json), ContentType = "application/json" } },
                },
            },
        };

        message.RemoteConfigStatus = new RemoteConfigStatus()
        {
            ErrorMessage = "Error Message Here",
            LastRemoteConfigHash = ByteString.CopyFromUtf8("test-set-1"),
            Status = RemoteConfigStatuses.Applied,
        };

        message.PackageStatuses = new PackageStatuses()
        {
            ErrorMessage = "Error Message Here",
        };

        // message.AgentDisconnect = new AgentDisconnect()
        // {

        // };

        // message.Flags = (ulong)AgentToServerFlags.RequestInstanceUid;

        await this.transport.SendAsync(message, this.token).ConfigureAwait(false);
    }

    private ulong IncrementSequenceNum() => Interlocked.Increment(ref this.sequenceNum);
}
