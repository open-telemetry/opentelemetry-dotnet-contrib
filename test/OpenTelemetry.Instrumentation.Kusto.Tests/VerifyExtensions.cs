// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

internal static class VerifyExtensions
{
    public static SettingsTask ScrubHostname(this SettingsTask settings, string hostname) =>
        settings.ScrubLinesWithReplace(line => line.Replace(hostname, "Scrubbed"));

    public static SettingsTask ScrubPort(this SettingsTask settings, int port) =>
        settings
        .ScrubLinesWithReplace(line => line.Replace(port.ToString(), "Scrubbed"))
        .ScrubInstance<int>(i => i == port);
}
