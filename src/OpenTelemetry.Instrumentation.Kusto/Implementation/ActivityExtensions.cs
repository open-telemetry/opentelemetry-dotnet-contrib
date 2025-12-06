// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Extensions on for <see cref="Activity"/>.
/// </summary>
internal static class ActivityExtensions
{
    /// <summary>
    /// Adds the specified tags to the activity if they do not already exist.
    /// </summary>
    /// <remarks>
    /// This method does not overwrite existing tags on the activity. Only tags with keys not already
    /// present are added.
    /// </remarks>
    /// <param name="activity">The activity to which tags will be added.</param>
    /// <param name="tags">
    /// The collection of tags to add to the activity. Each tag is added only if its key does not already exist on the
    /// activity.
    /// </param>
    /// <returns>
    /// The activity instance with the new tags added, or unchanged if all tag keys already exist.
    /// </returns>
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
