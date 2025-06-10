// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Opamp.Protocol;
using OpenTelemetry.OpAMPClient.Transport;
using OpenTelemetry.OpAMPClient.Trash;

namespace OpenTelemetry.OpAMPClient;

internal class OpAMPClient
{
    private readonly ByteString instanceUid = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());

    private readonly FrameProcessor processor = new(new SampleMessageListener());
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    // TODO: remove this warning suppression once we have a public API in place
    private readonly IOpAMPTransport transport;
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    private CancellationToken token;
    private ulong sequenceNum;

    public OpAMPClient(IOpAMPTransport transport)
    {
        // this.transport = new WsTransport(this.processor);
        this.transport = new HttpTransport(this.processor);
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        this.token = token;

        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StartAsync(token).ConfigureAwait(false);
        }

        await this.SendIdentificationAsync().ConfigureAwait(false);
    }

    public async Task SendIdentificationAsync()
    {
        var message = new AgentToServer()
        {
            InstanceUid = this.instanceUid,
            SequenceNum = this.sequenceNum++,
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
}
