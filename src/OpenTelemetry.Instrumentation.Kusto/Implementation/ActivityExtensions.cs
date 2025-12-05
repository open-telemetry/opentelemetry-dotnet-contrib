// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class ActivityExtensions
{
    public static Activity AddTags(this Activity activity, TagList tags)
    {
        foreach (var tag in tags)
        {
            if (activity.GetTagItem(tag.Key) is null)
            {
                activity.AddTag(tag.Key, tag.Value);
            }
        }

        return activity;
    }
}
