// <copyright file="EventNameManager.cs" company="OpenTelemetry Authors">
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

using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class EventNameManager
{
    private const int MinimumEventFullNameLength = 4;
    private const int MaximumEventFullNameLength = 100;
    private static readonly Regex EventNamespaceValidationRegex = new(@"^[A-Za-z](?:\.?[A-Za-z0-9]+?)*$", RegexOptions.Compiled);
    private static readonly Regex EventNameValidationRegex = new(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private readonly string defaultEventNamespace;
    private readonly string defaultEventName;
    private readonly byte[] defaultEventFullName;
    private readonly Hashtable eventNamespaceCache = new(StringComparer.OrdinalIgnoreCase);

    public EventNameManager(string defaultEventNamespace, string defaultEventName)
    {
        Debug.Assert(defaultEventNamespace != null, "defaultEventNamespace was null");
        Debug.Assert(defaultEventName != null, "defaultEventName was null");

        this.defaultEventNamespace = defaultEventNamespace!;
        this.defaultEventName = defaultEventName!;

        if (!IsEventNamespaceValid(defaultEventNamespace!))
        {
            throw new ArgumentException($"Default event namespace '{defaultEventNamespace}' was invalid.", nameof(defaultEventNamespace));
        }

        if (!IsEventNamespaceValid(defaultEventName!))
        {
            throw new ArgumentException($"Default event name '{defaultEventName}' was invalid.", nameof(defaultEventName));
        }

        var defaultEventFullNameLength = defaultEventNamespace!.Length + defaultEventName!.Length + 1;
        if (defaultEventFullNameLength < MinimumEventFullNameLength || defaultEventFullNameLength > MaximumEventFullNameLength)
        {
            throw new ArgumentException($"Default event full name '{defaultEventNamespace}.{defaultEventName}' does not meet length requirements.", nameof(defaultEventName));
        }

        this.defaultEventFullName = BuildEventFullName(defaultEventNamespace, defaultEventName)!;

#if NET6_0_OR_GREATER
        Debug.Assert(this.defaultEventFullName != null, "this.defaultFullyQualifiedEventName was null");
#endif
    }

    // Note: This is exposed for unit tests.
    internal Hashtable EventNamespaceCache => this.eventNamespaceCache;

    public static bool IsEventNamespaceValid(string eventNamespace)
        => EventNamespaceValidationRegex.IsMatch(eventNamespace);

    public static bool IsEventNameValid(string eventName)
        => EventNameValidationRegex.IsMatch(eventName);

    public ReadOnlySpan<byte> ResolveEventFullName(
        string? eventNamespace,
        string? eventName)
    {
        var eventNameIsNullOrWhiteSpace = string.IsNullOrWhiteSpace(eventName);

        if (string.IsNullOrWhiteSpace(eventNamespace))
        {
            if (eventNameIsNullOrWhiteSpace)
            {
                return this.defaultEventFullName;
            }

            eventNamespace = this.defaultEventNamespace;
        }

        if (eventNameIsNullOrWhiteSpace)
        {
            eventName = this.defaultEventName;
        }

        var eventNameCache = this.GetEventNameCacheForEventNamespace(eventNamespace!);

        if (eventNameCache[eventName!] is byte[] cachedEventFullName)
        {
            return cachedEventFullName;
        }

        return this.ResolveEventNameRare(eventNameCache, eventNamespace!, eventName!);
    }

    private static byte[] BuildEventFullName(string eventNamespace, string eventName)
    {
        Span<byte> destination = stackalloc byte[128];

        destination[0] = (byte)'\"';

        var cursor = 1;

        WriteEventFullNameComponent(eventNamespace, destination, ref cursor);

        destination[cursor++] = (byte)'.';

        WriteEventFullNameComponent(eventName, destination, ref cursor);

        destination[cursor++] = (byte)'\"';

        return destination.Slice(0, cursor).ToArray();
    }

    private static void WriteEventFullNameComponent(string component, Span<byte> destination, ref int cursor)
    {
        char firstChar = component[0];
        if (firstChar >= 'a' && firstChar <= 'z')
        {
            firstChar -= (char)32;
        }

        destination[cursor++] = (byte)firstChar;

        for (int i = 1; i < component.Length; i++)
        {
            destination[cursor++] = (byte)component[i];
        }
    }

    private Hashtable GetEventNameCacheForEventNamespace(string eventNamespace)
    {
        var eventNamespaceCache = this.eventNamespaceCache;

        if (eventNamespaceCache[eventNamespace] is not Hashtable eventNameCacheForNamespace)
        {
            lock (eventNamespaceCache)
            {
                eventNameCacheForNamespace = (eventNamespaceCache[eventNamespace] as Hashtable)!;
                if (eventNameCacheForNamespace == null)
                {
                    eventNameCacheForNamespace = new Hashtable(StringComparer.OrdinalIgnoreCase);
                    eventNamespaceCache[eventNamespace] = eventNameCacheForNamespace;
                }
            }
        }

        return eventNameCacheForNamespace;
    }

    private byte[] ResolveEventNameRare(Hashtable eventNameCache, string eventNamespace, string eventName)
    {
        if (!IsEventNamespaceValid(eventNamespace))
        {
            OneCollectorExporterEventSource.Log.EventNamespaceInvalid(eventNamespace);
            eventNamespace = this.defaultEventNamespace;
        }

        var eventNameHashtableKey = eventName;

        if (!IsEventNameValid(eventName))
        {
            OneCollectorExporterEventSource.Log.EventNameInvalid(eventName);
            eventName = this.defaultEventName;
        }

        byte[] eventFullName;

        var finalEventFullNameLength = eventNamespace.Length + eventName.Length + 1;
        if (finalEventFullNameLength < MinimumEventFullNameLength || finalEventFullNameLength > MaximumEventFullNameLength)
        {
            OneCollectorExporterEventSource.Log.EventFullNameDiscarded(eventNamespace, eventName);
            eventFullName = this.defaultEventFullName;
        }
        else
        {
            eventFullName = BuildEventFullName(eventNamespace!, eventName!);
        }

        lock (eventNameCache)
        {
            if (eventNameCache[eventNameHashtableKey] is null)
            {
                eventNameCache[eventNameHashtableKey] = eventFullName;
            }
        }

        return eventFullName;
    }
}
