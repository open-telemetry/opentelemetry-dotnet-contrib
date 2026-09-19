// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Internal.Utils;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class FrameBuilder : IFrameBuilder
{
    private readonly OpAmpClientSettings settings;

    private AgentToServer currentMessage;
    private ByteString instanceUid;
    private ulong sequenceNum;

    public FrameBuilder(OpAmpClientSettings settings)
    {
        this.settings = settings;
        this.instanceUid = ByteString.CopyFrom(this.settings.InstanceUid.ToByteArray());
        this.currentMessage = this.NextBaseMessage();
    }

    IFrameBuilder IFrameBuilder.AddAgentDescription()
    {
        var resources = this.settings.Identification;
        var description = new AgentDescription();

        foreach (var resource in resources.IdentifyingResources)
        {
            description.IdentifyingAttributes.Add(new KeyValue()
            {
                Key = resource.Key,
                Value = resource.Value.ToAnyValue(),
            });
        }

        foreach (var resource in resources.NonIdentifyingResources)
        {
            description.NonIdentifyingAttributes.Add(new KeyValue()
            {
                Key = resource.Key,
                Value = resource.Value.ToAnyValue(),
            });
        }

        this.currentMessage.AgentDescription = description;

        return this;
    }

    IFrameBuilder IFrameBuilder.AddHealth(HealthReport health)
    {
        this.currentMessage.Health = new ComponentHealth()
        {
            Healthy = health.IsHealthy,
            StartTimeUnixNano = health.StartTime,
            StatusTimeUnixNano = health.StatusTime,
        };

        if (health.Status != null)
        {
            this.currentMessage.Health.Status = health.Status;
        }

        if (health.LastError != null)
        {
            this.currentMessage.Health.LastError = health.LastError;
        }

        foreach (var item in health.Components)
        {
            var component = new ComponentHealth()
            {
                Healthy = item.IsHealthy,
                StartTimeUnixNano = (ulong)item.StartTime.ToUnixTimeMilliseconds() * 1_000_000, // Convert to nanoseconds
                StatusTimeUnixNano = (ulong)item.StatusTime.ToUnixTimeMilliseconds() * 1_000_000, // Convert to nanoseconds
            };

            if (health.Status != null)
            {
                component.Status = health.Status;
            }

            if (health.LastError != null)
            {
                component.LastError = health.LastError;
            }

            this.currentMessage.Health.ComponentHealthMap[item.ComponentName] = component;
        }

        return this;
    }

    IFrameBuilder IFrameBuilder.AddAgentDisconnect()
    {
        this.currentMessage.AgentDisconnect = new AgentDisconnect();

        return this;
    }

    IFrameBuilder IFrameBuilder.AddCapabilities()
    {
        // TODO: Update the actual capabilities when features are implemented.

        var capabilities = AgentCapabilities.ReportsStatus;

        if (this.settings.Heartbeat.IsEnabled)
        {
            capabilities |= AgentCapabilities.ReportsHeartbeat | AgentCapabilities.ReportsHealth;
        }

        if (this.settings.RemoteConfiguration.AcceptsRemoteConfig)
        {
            capabilities |= AgentCapabilities.AcceptsRemoteConfig;
        }

        if (this.settings.RemoteConfiguration.ReportsRemoteConfigStatus)
        {
            capabilities |= AgentCapabilities.ReportsRemoteConfig;
        }

        if (this.settings.EffectiveConfigurationReporting.EnableReporting)
        {
            capabilities |= AgentCapabilities.ReportsEffectiveConfig;
        }

        this.currentMessage.Capabilities = (ulong)capabilities;

        return this;
    }

    IFrameBuilder IFrameBuilder.AddCustomCapabilities(IEnumerable<string> capabilities)
    {
        this.currentMessage.CustomCapabilities = new CustomCapabilities();
        this.currentMessage.CustomCapabilities.Capabilities.Add(capabilities);

        return this;
    }

    IFrameBuilder IFrameBuilder.AddCustomMessage(string capability, string type, ReadOnlyMemory<byte> data)
    {
        this.currentMessage.CustomMessage = new CustomMessage
        {
            Capability = capability,
            Type = type,
            Data = ByteString.CopyFrom(data.Span),
        };

        return this;
    }

    IFrameBuilder IFrameBuilder.AddEffectiveConfig(IEnumerable<EffectiveConfigFile> files)
    {
        var configMap = new AgentConfigMap();
        var fileMap = new Dictionary<string, global::OpAmp.Proto.V1.AgentConfigFile>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (fileMap.ContainsKey(file.FileName))
            {
                throw new ArgumentException($"Multiple config files share the same FileName '{file.FileName}'. FileNames must be unique.", nameof(files));
            }

            fileMap.Add(file.FileName, new global::OpAmp.Proto.V1.AgentConfigFile()
            {
                Body = ByteString.CopyFrom(file.Content.Span),
                ContentType = file.ContentType,
            });
        }

        configMap.ConfigMap.Add(fileMap);

        var effectiveConfig = new EffectiveConfig
        {
            ConfigMap = configMap,
        };

        this.currentMessage.EffectiveConfig = effectiveConfig;

        return this;
    }

    IFrameBuilder IFrameBuilder.AddRemoteConfigStatus(RemoteConfigStatusReport status)
    {
        this.currentMessage.RemoteConfigStatus = status.ToRemoteConfigStatus();

        return this;
    }

    public AgentToServer Build()
    {
        var message = this.currentMessage;
        this.currentMessage = this.NextBaseMessage();

        return message;
    }

    private AgentToServer NextBaseMessage()
    {
        var message = new AgentToServer()
        {
            InstanceUid = this.instanceUid,
            SequenceNum = ++this.sequenceNum,
        };

        return message;
    }
}
