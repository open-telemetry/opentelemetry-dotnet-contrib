// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
#if NET
using System.Diagnostics;
#endif
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed partial class EventNameManager
{
    // Note: OneCollector will silently drop events which have a name less than 4 characters.
    internal const int MinimumEventFullNameLength = 4;
    internal const int MaximumEventFullNameLength = 100;

    private readonly string defaultEventNamespace;
    private readonly string defaultEventName;
    private readonly IReadOnlyDictionary<string, EventFullName>? eventFullNameMappings;
    private readonly ResolvedEventFullName defaultEventFullName;

    public EventNameManager(
        string defaultEventNamespace,
        string defaultEventName,
        IReadOnlyDictionary<string, EventFullName>? eventFullNameMappings = null)
    {
        this.defaultEventNamespace = defaultEventNamespace;
        this.defaultEventName = defaultEventName;
        this.eventFullNameMappings = eventFullNameMappings;

        this.defaultEventFullName = new(
            eventFullName: BuildEventFullName(this.defaultEventNamespace, this.defaultEventName),
            originalEventNamespace: null,
            originalEventName: null);

#if NET
        Debug.Assert(this.defaultEventFullName.EventFullName != null, "this.defaultFullyQualifiedEventName was null");
#endif
    }

    // Note: These caches are exposed for unit tests.
    internal Hashtable EventNamespaceCache { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Hashtable EventFullNameCache { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsEventNamespaceValid(string eventNamespace)
        => EventNamespaceValidationRegex().IsMatch(eventNamespace);

    public static bool IsEventNameValid(string eventName)
        => EventNameValidationRegex().IsMatch(eventName);

    public ResolvedEventFullName ResolveEventFullName(
        string eventFullName)
    {
        if (this.EventFullNameCache[eventFullName] is ResolvedEventFullName cachedEventFullName)
        {
            return cachedEventFullName;
        }

        var eventFullNameBlob = BuildJsonStringValue(eventFullName);

        var resolvedEventFullName = new ResolvedEventFullName(
            eventFullNameBlob,
            originalEventNamespace: null,
            originalEventName: null);

        lock (this.EventFullNameCache)
        {
            if (this.EventFullNameCache[eventFullName] is null)
            {
                this.EventFullNameCache[eventFullName] = resolvedEventFullName;
            }
        }

        return resolvedEventFullName;
    }

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

        // Note: ResolveEventNameRare may replace eventNamespace and eventName so
        // the values used for the lookup are captured to make sure the result is
        // cached under the same key and can be compared on later lookups.
        var lookupEventNamespace = eventNamespace!;
        var eventNameCacheKey = eventName!;

        // Note: A non-empty whitespace namespace or name is replaced by the
        // default above but its original value is still preserved, so such
        // inputs can't reuse an entry shared by case variants.
        var inputReplaced = (!string.IsNullOrEmpty(originalEventNamespace) && !string.Equals(originalEventNamespace, lookupEventNamespace, StringComparison.Ordinal))
            || (!string.IsNullOrEmpty(originalEventName) && !string.Equals(originalEventName, eventNameCacheKey, StringComparison.Ordinal));

        if (eventNameCache[eventNameCacheKey] is ResolvedEventFullName cachedEventFullName
            && cachedEventFullName.IsMatch(originalEventNamespace, originalEventName, inputReplaced))
        {
            return cachedEventFullName;
        }

        var eventFullNameBlob = this.ResolveEventNameRare(
            ref eventNamespace!,
            ref eventName!);

        var preserveOriginalEventNamespace = !string.IsNullOrEmpty(originalEventNamespace) && originalEventNamespace != eventNamespace;
        var preserveOriginalEventName = !string.IsNullOrEmpty(originalEventName) && originalEventName != eventName;

        // Note: The caches are case-insensitive. When mappings are configured or a
        // default replaced the namespace or name, the result (including which
        // original values are preserved) depends on the exact casing, even when a
        // mapping produced the same value as the input, so the entry is only
        // reused for an exact match. Otherwise case variants share the entry.
        var requiresExactMatch = this.eventFullNameMappings != null
            || inputReplaced
            || !string.Equals(lookupEventNamespace, eventNamespace, StringComparison.Ordinal)
            || !string.Equals(eventNameCacheKey, eventName, StringComparison.Ordinal);

        var resolvedEventFullName = new ResolvedEventFullName(
            eventFullNameBlob,
            preserveOriginalEventNamespace ? BuildJsonStringValue(originalEventNamespace!) : null,
            preserveOriginalEventName ? BuildJsonStringValue(originalEventName!) : null,
            requiresExactMatch,
            originalEventNamespace,
            originalEventName);

        lock (eventNameCache)
        {
            // Note: When a different casing which requires an exact match is
            // already cached the result is returned without being cached.
            if (eventNameCache[eventNameCacheKey] is null)
            {
                eventNameCache[eventNameCacheKey] = resolvedEventFullName;
            }
        }

        return resolvedEventFullName;
    }

    // Note: The namespace pattern avoids nested quantifiers so validation runs
    // in linear time for any input. \z is used instead of $ so a trailing newline
    // is not accepted.
#if NET
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)*\z")]
    private static partial Regex EventNamespaceValidationRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*\z")]

    private static partial Regex EventNameValidationRegex();
#else

#pragma warning disable SA1201 // A field should not follow a method
    private static readonly Regex EventNamespaceValidationRegexField = new(@"^[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)*\z", RegexOptions.Compiled);
    private static readonly Regex EventNameValidationRegexField = new(@"^[A-Za-z][A-Za-z0-9]*\z", RegexOptions.Compiled);
#pragma warning restore SA1201 // A field should not follow a method

    private static Regex EventNamespaceValidationRegex() => EventNamespaceValidationRegexField;

    private static Regex EventNameValidationRegex() => EventNameValidationRegexField;
#endif

    private static byte[] BuildEventFullName(string eventNamespace, string eventName)
    {
        // Note: Only called for names which passed validation (ASCII letters,
        // digits, and '.') and fit in MaximumEventFullNameLength so they can be
        // copied as-is without JSON escaping.
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

    private static byte[] BuildJsonStringValue(string value)
    {
        // Note: Used for names which have not been validated (for example original
        // names which were replaced by a default or a mapping) so the value may be
        // any length and contain any characters and must be JSON encoded.
        var firstChar = value[0];
        if (char.IsAsciiLetterLower(firstChar))
        {
            value = (char)(firstChar - 32) + value.Substring(1);
        }

        using var stream = new MemoryStream(value.Length + 2);

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStringValue(value);
        }

        return stream.ToArray();
    }

    private static void WriteEventFullNameComponent(string component, Span<byte> destination, ref int cursor)
    {
        var firstChar = component[0];
        if (char.IsAsciiLetterLower(firstChar))
        {
            firstChar -= (char)32;
        }

        destination[cursor++] = (byte)firstChar;

        for (var i = 1; i < component.Length; i++)
        {
            destination[cursor++] = (byte)component[i];
        }
    }

    private Hashtable GetEventNameCacheForEventNamespace(string eventNamespace)
    {
        var eventNamespaceCache = this.EventNamespaceCache;

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
        if (finalEventFullNameLength is < MinimumEventFullNameLength or > MaximumEventFullNameLength)
        {
            OneCollectorExporterEventSource.Log.EventFullNameDiscarded(eventNamespace, eventName);
            eventFullName = this.defaultEventFullName.EventFullName;
        }
        else
        {
            eventFullName = BuildEventFullName(eventNamespace, eventName);
        }

        return eventFullName;
    }

    internal sealed class ResolvedEventFullName
    {
        private readonly bool requiresExactMatch;
        private readonly string? eventNamespace;
        private readonly string? eventName;

        public ResolvedEventFullName(
            byte[] eventFullName,
            byte[]? originalEventNamespace,
            byte[]? originalEventName,
            bool requiresExactMatch = false,
            string? eventNamespace = null,
            string? eventName = null)
        {
            this.EventFullName = eventFullName;
            this.OriginalEventNamespace = originalEventNamespace;
            this.OriginalEventName = originalEventName;
            this.requiresExactMatch = requiresExactMatch;
            this.eventNamespace = eventNamespace;
            this.eventName = eventName;
        }

        public byte[] EventFullName { get; }

        public byte[]? OriginalEventNamespace { get; }

        public byte[]? OriginalEventName { get; }

        public bool IsMatch(string? eventNamespace, string? eventName, bool inputReplaced)
            => this.requiresExactMatch
                ? string.Equals(this.eventNamespace, eventNamespace, StringComparison.Ordinal)
                    && string.Equals(this.eventName, eventName, StringComparison.Ordinal)
                : !inputReplaced;
    }
}
