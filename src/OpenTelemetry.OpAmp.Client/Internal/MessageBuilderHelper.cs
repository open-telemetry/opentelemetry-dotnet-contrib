// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal static class MessageBuilderHelper
{
    public static void AppendIdentification(IFrameBuilder fb) => fb
            .AddAgentDescription()
            .AddCapabilities();

    public static void AppendFullStateReport(IFrameBuilder fb, FullStateReport report)
    {
        AppendIdentification(fb);

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
    }
}
