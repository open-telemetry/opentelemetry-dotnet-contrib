// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;

namespace OpenTelemetry.Trace;

internal static class ActivityHelperExtensions
{
    public static object? GetTagValue(this Activity activity, string tagName)
    {
        foreach (var tag in activity.TagObjects)
        {
            if (tag.Key == tagName)
            {
                return tag.Value;
            }
        }

        return null;
    }
}
