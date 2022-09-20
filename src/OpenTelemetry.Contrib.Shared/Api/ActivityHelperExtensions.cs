// <copyright file="ActivityHelperExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods on Activity.
/// </summary>
internal static class ActivityHelperExtensions
{
    /// <summary>
    /// Gets the value of a specific tag on an <see cref="Activity"/>.
    /// </summary>
    /// <param name="activity">Activity instance.</param>
    /// <param name="tagName">Case-sensitive tag name to retrieve.</param>
    /// <returns>Tag value or null if a match was not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
    public static object GetTagValue(this Activity activity, string tagName)
    {
        Debug.Assert(activity != null, "Activity should not be null");

        // TODO: Update to use
        // https://docs.microsoft.com/dotnet/api/system.diagnostics.activity.enumeratetagobjects?view=net-7.0
        // instead of reflection. See:
        // https://github.com/open-telemetry/opentelemetry-dotnet/blob/928d77056c1d353d8ba72ad3b4b565398117b352/src/OpenTelemetry.Api/Internal/ActivityHelperExtensions.cs#L85-L98

        ActivitySingleTagEnumerator state = new ActivitySingleTagEnumerator(tagName);

        ActivityTagsEnumeratorFactory<ActivitySingleTagEnumerator>.Enumerate(activity, ref state);

        return state.Value;
    }

    private struct ActivitySingleTagEnumerator : IActivityEnumerator<KeyValuePair<string, object>>
    {
        public object Value;

        private readonly string tagName;

        public ActivitySingleTagEnumerator(string tagName)
        {
            this.tagName = tagName;
            this.Value = null;
        }

        public bool ForEach(KeyValuePair<string, object> item)
        {
            if (item.Key == this.tagName)
            {
                this.Value = item.Value;
                return false;
            }

            return true;
        }
    }

    private static class ActivityTagsEnumeratorFactory<TState>
        where TState : struct, IActivityEnumerator<KeyValuePair<string, object>>
    {
        private static readonly object EmptyActivityTagObjects = typeof(Activity).GetField("s_emptyTagObjects", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static readonly DictionaryEnumerator<string, object, TState>.AllocationFreeForEachDelegate
            ActivityTagObjectsEnumerator = DictionaryEnumerator<string, object, TState>.BuildAllocationFreeForEachDelegate(
                typeof(Activity).GetField("_tags", BindingFlags.Instance | BindingFlags.NonPublic).FieldType);

        private static readonly DictionaryEnumerator<string, object, TState>.ForEachDelegate ForEachTagValueCallbackRef = ForEachTagValueCallback;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Enumerate(Activity activity, ref TState state)
        {
            var tagObjects = activity.TagObjects;

            if (ReferenceEquals(tagObjects, EmptyActivityTagObjects))
            {
                return;
            }

            ActivityTagObjectsEnumerator(
                tagObjects,
                ref state,
                ForEachTagValueCallbackRef);
        }

        private static bool ForEachTagValueCallback(ref TState state, KeyValuePair<string, object> item)
            => state.ForEach(item);
    }
}
