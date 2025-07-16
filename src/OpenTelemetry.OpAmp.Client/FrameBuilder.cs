// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Protocol;
using OpenTelemetry.OpAmp.Client.Services.Internal;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Trash;
using OpenTelemetry.OpAmp.Client.Utils;

namespace OpenTelemetry.OpAmp.Client;

internal class FrameBuilder : IFrameBuilder
{
    private readonly OpAmpSettings settings;

    private AgentToServer? currentMessage;
    private ByteString instanceUid;
    private ulong sequenceNum;

    public FrameBuilder(OpAmpSettings settings)
    {
        this.settings = settings;
        this.instanceUid = ByteString.CopyFrom(this.settings.InstanceUid.ToByteArray());
    }

    public IFrameBuilder StartBaseMessage()
    {
        if (this.currentMessage != null)
        {
            throw new InvalidOperationException("Message base is already initialized.");
        }

        var message = new AgentToServer()
        {
            InstanceUid = this.instanceUid,
            SequenceNum = ++this.sequenceNum,
        };

        this.currentMessage = message;
        return this;
    }

    IFrameBuilder IFrameBuilder.AddDescription()
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        var resources = this.settings.Resources;
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

        this.currentMessage.AgentDescription = description;

        return this;
    }

    IFrameBuilder IFrameBuilder.AddHeartbeat(HealthReport health)
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        this.currentMessage.Health = new ComponentHealth()
        {
            Healthy = health.DetailedStatus.IsHealthy,
            StartTimeUnixNano = health.StartTime,
            StatusTimeUnixNano = health.StatusTime,
        };

        if (health.DetailedStatus.Status != null)
        {
            this.currentMessage.Health.Status = health.DetailedStatus.Status;
        }

        if (health.DetailedStatus.LastError != null)
        {
            this.currentMessage.Health.LastError = health.DetailedStatus.LastError;
        }

        foreach (var item in health.DetailedStatus.Components)
        {
            var component = new ComponentHealth()
            {
                Healthy = item.IsHealthy,
                StartTimeUnixNano = (ulong)item.StartTime.ToUnixTimeMilliseconds() * 1000000, // Convert to nanoseconds
                StatusTimeUnixNano = (ulong)item.StatusTime.ToUnixTimeMilliseconds() * 1000000, // Convert to nanoseconds
            };

            if (health.DetailedStatus.Status != null)
            {
                component.Status = health.DetailedStatus.Status;
            }

            if (health.DetailedStatus.LastError != null)
            {
                component.LastError = health.DetailedStatus.LastError;
            }

            this.currentMessage.Health.ComponentHealthMap[item.ComponentName] = component;
        }

        return this;
    }

    IFrameBuilder IFrameBuilder.AddCapabilities()
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        // TODO: Update the actual capabilities when available
        this.currentMessage.Capabilities = (ulong)(AgentCapabilities.ReportsStatus
            | AgentCapabilities.ReportsHeartbeat
            | AgentCapabilities.ReportsHealth);

        return this;
    }

    IFrameBuilder IFrameBuilder.AddCurrentConfig()
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        // TODO: Pass the actual current config here
        this.currentMessage.EffectiveConfig = new EffectiveConfig()
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

        return this;
    }

    IFrameBuilder IFrameBuilder.AddConfigStatus()
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        // TODO: Pass the actual config status here
        this.currentMessage.RemoteConfigStatus = new RemoteConfigStatus()
        {
            ErrorMessage = "Error Message Here",
            LastRemoteConfigHash = ByteString.CopyFromUtf8("test-set-1"),
            Status = RemoteConfigStatuses.Applied,
        };

        return this;
    }

    IFrameBuilder IFrameBuilder.AddPackageStatus()
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        // TODO: Pass the actual package status here
        this.currentMessage.PackageStatuses = new PackageStatuses()
        {
            ErrorMessage = "Error Message Here",
        };

        return this;
    }

    IFrameBuilder IFrameBuilder.AddDisconnectRequest()
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        this.currentMessage.AgentDisconnect = new AgentDisconnect();

        return this;
    }

    IFrameBuilder IFrameBuilder.SetFlags()
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        // TODO: Set the actual flags based (currently the only flag available is RequestInstanceUid)
        this.currentMessage.Flags = (ulong)AgentToServerFlags.RequestInstanceUid;

        return this;
    }

    AgentToServer IFrameBuilder.Build()
    {
        if (this.currentMessage == null)
        {
            // TODO: consider unknown status, so that indicates that heartbeat service is working itself.
            throw new InvalidOperationException("Message base is not initialized.");
        }

        var message = this.currentMessage;
        this.currentMessage = null; // Reset for the next message

        return message;
    }

    public void Reset()
    {
        this.currentMessage = null;
    }
}
