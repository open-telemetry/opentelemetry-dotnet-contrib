// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class EventNameManager
{
    // Note: OneCollector will silently drop events which have a name less than 4 characters.
    internal const int MinimumEventFullNameLength = 4;
    internal const int MaximumEventFullNameLength = 100;
    private static readonly Regex EventNamespaceValidationRegex = new(@"^[A-Za-z](?:\.?[A-Za-z0-9]+?)*$", RegexOptions.Compiled);
    private static readonly Regex EventNameValidationRegex = new(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private readonly string defaultEventNamespace;
    private readonly string defaultEventName;
    private readonly IReadOnlyDictionary<string, EventFullName>? eventFullNameMappings;
    private readonly ResolvedEventFullName defaultEventFullName;
    private readonly Hashtable eventNamespaceCache = new(StringComparer.OrdinalIgnoreCase);

    public EventNameManager(
        string defaultEventNamespace,
        string defaultEventName,
        IReadOnlyDictionary<string, EventFullName>? eventFullNameMappings = null)
    {
        Debug.Assert(defaultEventNamespace != null, "defaultEventNamespace was null");
        Debug.Assert(defaultEventName != null, "defaultEventName was null");

        this.defaultEventNamespace = defaultEventNamespace!;
        this.defaultEventName = defaultEventName!;
        this.eventFullNameMappings = eventFullNameMappings;

        this.defaultEventFullName = new(
            eventFullName: BuildEventFullName(this.defaultEventNamespace, this.defaultEventName),
            originalEventNamespace: null,
            originalEventName: null);

#if NET
        Debug.Assert(this.defaultEventFullName.EventFullName != null, "this.defaultFullyQualifiedEventName was null");
#endif
    }

    // Note: This is exposed for unit tests.
    internal Hashtable EventNamespaceCache => this.eventNamespaceCache;

    public static bool IsEventNamespaceValid(string eventNamespace)
        => EventNamespaceValidationRegex.IsMatch(eventNamespace);

    public static bool IsEventNameValid(string eventName)
        => EventNameValidationRegex.IsMatch(eventName);

    public ResolvedEventFullName ResolveEventFullName(
        string? eventNamespace,
        string? eventName)
    {
        var originalEventNamespace = eventNamespace;
        var originalEventName = eventName;
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

        if (eventNameCache[eventName!] is ResolvedEventFullName cachedEventFullName)
        {
            return cachedEventFullName;
        }

        var eventFullNameBlob = this.ResolveEventNameRare(
            ref eventNamespace!,
            ref eventName!);

        byte[]? originalEventNamespaceBlob = !string.IsNullOrEmpty(originalEventNamespace)
            && originalEventNamespace != eventNamespace
            ? BuildEventFullName(string.Empty, originalEventNamespace!)
            : null;

        byte[]? originalEventNameBlob = !string.IsNullOrEmpty(originalEventName)
            && originalEventName != eventName
            ? BuildEventFullName(string.Empty, originalEventName!)
            : null;

        var resolvedEventFullName = new ResolvedEventFullName(
            eventFullNameBlob,
            originalEventNamespaceBlob,
            originalEventNameBlob);

        lock (eventNameCache)
        {
            if (eventNameCache[eventName!] is null)
            {
                eventNameCache[eventName!] = resolvedEventFullName;
            }
        }

        return resolvedEventFullName;
    }

    private static byte[] BuildEventFullName(string eventNamespace, string eventName)
    {
        Span<byte> destination = stackalloc byte[128];

        destination[0] = (byte)'\"';

        var cursor = 1;

        if (eventNamespace.Length > 0)
        {
            WriteEventFullNameComponent(eventNamespace, destination, ref cursor);

            destination[cursor++] = (byte)'.';
        }

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

    private byte[] ResolveEventNameRare(
        ref string eventNamespace,
        ref string eventName)
    {
        var originalNamespace = eventNamespace;
        var originalName = eventName;

        var eventFullNameMappings = this.eventFullNameMappings;
        if (eventFullNameMappings != null)
        {
            var tempEventFullName = $"{eventNamespace}.{eventName}";

            if (eventFullNameMappings.TryGetValue(
                tempEventFullName,
                out var exactMatchRule))
            {
                eventNamespace = exactMatchRule.EventNamespace;
                eventName = exactMatchRule.EventName;
            }
            else
            {
                KeyValuePair<string, EventFullName>? prefixMatchRule = null;

                foreach (var mappingRule in eventFullNameMappings)
                {
                    if (!tempEventFullName.StartsWith(mappingRule.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!prefixMatchRule.HasValue
                        || mappingRule.Key.Length >= prefixMatchRule.Value.Key.Length)
                    {
                        prefixMatchRule = mappingRule;
                    }
                }

                if (prefixMatchRule.HasValue)
                {
                    eventNamespace = prefixMatchRule.Value.Value.EventNamespace;
                    eventName = prefixMatchRule.Value.Value.EventName;
                }
                else if (eventFullNameMappings.TryGetValue("*", out var defaultRule))
                {
                    eventNamespace = defaultRule.EventNamespace;
                    eventName = defaultRule.EventName;
                }
                else
                {
                    eventNamespace = this.defaultEventNamespace;
                    eventName = this.defaultEventName;
                }
            }

            if (eventNamespace.Length == 0 && eventName == "*")
            {
                eventNamespace = originalNamespace;
                eventName = originalName;
            }
        }

        var namespaceLength = eventNamespace.Length;
        if (namespaceLength != 0)
        {
            if (!IsEventNamespaceValid(eventNamespace))
            {
                OneCollectorExporterEventSource.Log.EventNamespaceInvalid(eventNamespace);
                eventNamespace = this.defaultEventNamespace;
            }

            namespaceLength = eventNamespace.Length + 1;
        }

        if (!IsEventNameValid(eventName))
        {
            OneCollectorExporterEventSource.Log.EventNameInvalid(eventName);
            eventName = this.defaultEventName;
        }

        byte[] eventFullName;

        var finalEventFullNameLength = namespaceLength + eventName.Length;
        if (finalEventFullNameLength < MinimumEventFullNameLength || finalEventFullNameLength > MaximumEventFullNameLength)
        {
            OneCollectorExporterEventSource.Log.EventFullNameDiscarded(eventNamespace, eventName);
            eventFullName = this.defaultEventFullName.EventFullName;
        }
        else
        {
            eventFullName = BuildEventFullName(eventNamespace!, eventName!);
        }

        return eventFullName;
    }

    internal sealed class ResolvedEventFullName
    {
        public ResolvedEventFullName(
            byte[] eventFullName,
            byte[]? originalEventNamespace,
            byte[]? originalEventName)
        {
            this.EventFullName = eventFullName;
            this.OriginalEventNamespace = originalEventNamespace;
            this.OriginalEventName = originalEventName;
        }

        public byte[] EventFullName { get; }

        public byte[]? OriginalEventNamespace { get; }

        public byte[]? OriginalEventName { get; }
    }
}
