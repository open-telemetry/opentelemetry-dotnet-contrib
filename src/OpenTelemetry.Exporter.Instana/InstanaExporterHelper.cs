// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Instana;

internal sealed class InstanaExporterHelper : IInstanaExporterHelper
{
    public Resource GetParentProviderResource(BaseExporter<Activity> otelExporter)
        => otelExporter.ParentProvider.GetResource();

    public bool IsWindows() =>
#if NET
        OperatingSystem.IsWindows();
#else
        Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif
}
