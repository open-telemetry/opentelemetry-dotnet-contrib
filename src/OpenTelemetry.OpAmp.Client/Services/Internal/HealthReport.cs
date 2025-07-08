// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Data;

namespace OpenTelemetry.OpAmp.Client.Services.Internal;

internal class HealthReport
{
    public ulong StartTime { get; set; }

    public ulong StatusTime { get; set; }

    // TODO: Consider remapping to internal type
    public required HealthStatus DetailedStatus { get; set; }
}
