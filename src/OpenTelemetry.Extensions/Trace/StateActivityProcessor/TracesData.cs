// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

public class TracesData
{
    public List<ResourceSpans> ResourceSpans { get; set; } = [];

    public enum Signal
    {
        Start,
        Stop
    }

    public TracesData(Activity activity, Signal signal)
    {
        this.ResourceSpans.Add(new ResourceSpans(activity, signal));
    }
}
