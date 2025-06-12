// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Opamp.Protocol;
using OpenTelemetry.OpAMPClient.Data;
using OpenTelemetry.OpAMPClient.Settings;
using OpenTelemetry.OpAMPClient.Transport;
using OpenTelemetry.OpAMPClient.Trash;
using OpenTelemetry.OpAMPClient.Utils;

namespace OpenTelemetry.OpAMPClient;

/// <summary>
/// OpAMP client implementation that connects to an OpAMP server.
/// </summary>
public class OpAMPClient
{
    private readonly ByteString instanceUid = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());
    private readonly FrameProcessor processor = new(new SampleMessageListener());
    private readonly OpAMPSettings settings = new();
    private readonly IOpAMPTransport transport;
    private ulong sequenceNum;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpAMPClient"/> class.
    /// </summary>
    /// <param name="configure">Configure OpAmp settings</param>
    public OpAMPClient(Action<OpAMPSettings>? configure = null)
    {
        configure?.Invoke(this.settings);

        this.transport = ConstructTransport(this.settings.ConnectionType, this.processor);
    }

    /// <summary>
    /// Starts the asynchronous operation to initialize the transport and send identification data.
    /// </summary>
    /// <param name="token">A <see cref="CancellationToken"/> that can be used to cancel the operation. Defaults to <see
    /// langword="default"/> if not provided.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken token = default)
    {
        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StartAsync(token).ConfigureAwait(false);
        }

        await this.SendIdentificationAsync(this.settings, token).ConfigureAwait(false);
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

    private static AgentDescription CreateAgentDescription(OpAMPClientResources resources)
    {
        var description = new AgentDescription();

        foreach (var resource in resources.IdentifingResources)
        {
            description.IdentifyingAttributes.Add(new KeyValue()
            {
                Key = resource.Key,
                Value = resource.Value.ToAnyValue(),
            });
        }

        foreach (var resource in resources.NonIdentifingResources)
        {
            description.NonIdentifyingAttributes.Add(new KeyValue()
            {
                Key = resource.Key,
                Value = resource.Value.ToAnyValue(),
            });
        }

        return description;
    }

    private async Task SendIdentificationAsync(OpAMPSettings settings, CancellationToken token)
    {
        var message = new AgentToServer()
        {
            InstanceUid = this.instanceUid,
            SequenceNum = this.sequenceNum++,
        };

        message.AgentDescription = CreateAgentDescription(settings.Resources);

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

        await this.transport.SendAsync(message, token).ConfigureAwait(false);
    }
}
