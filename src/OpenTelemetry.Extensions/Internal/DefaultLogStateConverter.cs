// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Logs;

internal static class DefaultLogStateConverter
{
    public static void ConvertState(ActivityTagsCollection tags, IReadOnlyList<KeyValuePair<string, object?>> state)
    {
        for (int i = 0; i < state.Count; i++)
        {
            KeyValuePair<string, object?> stateItem = state[i];

            object? value = stateItem.Value;
            if (value != null)
            {
                if (string.IsNullOrEmpty(stateItem.Key))
                {
                    tags["state"] = value;
                }
                else
                {
                    tags[$"state.{stateItem.Key}"] = value;
                }
            }
        }
    }

    public static void ConvertScope(ActivityTagsCollection tags, int depth, LogRecordScope scope)
    {
        string prefix = $"scope[{depth}]";

        foreach (KeyValuePair<string, object?> scopeItem in scope)
        {
            object? value = scopeItem.Value;
            if (value != null)
            {
                if (string.IsNullOrEmpty(scopeItem.Key))
                {
                    tags[prefix] = value;
                }
                else
                {
                    tags[$"{prefix}.{scopeItem.Key}"] = value;
                }
            }
        }
    }
}
