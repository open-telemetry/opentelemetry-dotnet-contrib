// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal static class MessageBuilderHelper
{
    public static void AppendIdentification(IFrameBuilder fb)
    {
        AppendIdentificationCore(fb);
        OpAmpClientEventSource.Log.QueueingIdentificationMessage();
    }

    public static void AppendAgentDisconnect(IFrameBuilder fb)
    {
        fb.AddAgentDisconnect();
        OpAmpClientEventSource.Log.QueueingAgentDisconnectMessage();
    }

    public static Action<IFrameBuilder> AppendHeartbeat(HealthReport report) => fb =>
    {
        fb.AddHealth(report);
        OpAmpClientEventSource.Log.QueueingHeartbeatMessage();
    };

    public static Action<IFrameBuilder> AppendEffectiveConfig(IEnumerable<EffectiveConfigFile> files) => fb =>
    {
        fb.AddEffectiveConfig(files);
        OpAmpClientEventSource.Log.QueueingEffectiveConfigMessage();
    };

    public static Action<IFrameBuilder> AppendRemoteConfigStatus(RemoteConfigStatusReport status) => fb =>
    {
        fb.AddRemoteConfigStatus(status);
        OpAmpClientEventSource.Log.QueueingRemoteConfigStatusMessage();
    };

    public static Action<IFrameBuilder> AppendCustomCapabilities(IEnumerable<string> capabilities) => fb =>
    {
        fb.AddCustomCapabilities(capabilities);
        OpAmpClientEventSource.Log.QueueingCustomCapabilitiesMessage();
    };

    public static void AppendCustomMessage(
        IFrameBuilder fb,
        string capability,
        string type,
        ReadOnlyMemory<byte> data)
    {
        fb.AddCustomMessage(capability, type, data);
        OpAmpClientEventSource.Log.QueueingCustomMessageMessage();
    }

    public static Action<IFrameBuilder> AppendFullStateReport(FullStateReport report) => fb => AppendFullStateReport(fb, report);

    public static void AppendFullStateReport(IFrameBuilder fb, FullStateReport report)
    {
        AppendIdentificationCore(fb);

        // TODO: Add here features when they become available and are necessary to restore the full state in the server if requested.
        // See https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/4634
        if (report.EffectiveConfigFiles is { } effectiveConfig)
        {
            fb.AddEffectiveConfig(effectiveConfig);
        }

        if (report.RemoteConfigStatus is { } remoteConfigStatus)
        {
            fb.AddRemoteConfigStatus(remoteConfigStatus);
        }

        if (report.CustomCapabilities is { } customCapabilities)
        {
            fb.AddCustomCapabilities(customCapabilities);
        }

        if (report.HealthReport is { } healthReport)
        {
            fb.AddHealth(healthReport);
        }

        OpAmpClientEventSource.Log.QueueingFullStateReportMessage();
    }

    private static void AppendIdentificationCore(IFrameBuilder fb) => fb
            .AddAgentDescription()
            .AddCapabilities();
}
