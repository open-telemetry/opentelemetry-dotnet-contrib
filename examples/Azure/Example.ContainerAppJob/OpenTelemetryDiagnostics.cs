// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace Example.ContainerAppJob;

public static class OpenTelemetryDiagnostics
{
    public const string SourceName = "Example.ContainerAppJob";
    public static readonly ActivitySource ActivitySource = new ActivitySource(SourceName);
}
