// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// Span processor that adds <see cref="Baggage"/> fields to every new span
/// </summary>
public class BaggageSpanProcessor : OpenTelemetry.<Activity>
{
    /// <inheritdoc />
    public override void OnStart(Activity activity)
    {
        foreach (var entry in Baggage.Current)
        {
            activity.SetTag(entry.Key, entry.Value);
        }
    }
}
