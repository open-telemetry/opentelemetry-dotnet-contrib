// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed partial class EventNameManager
{
    // Note: OneCollector will silently drop events which have a name less than 4 characters.
    internal const int MinimumEventFullNameLength = 4;
    internal const int MaximumEventFullNameLength = 100;

    internal const int MaxNumberOfCachedEventFullNames = 2048;
    internal const int MaxNumberOfCachedEventNamespaces = 1024;
    internal const int MaxNumberOfCachedEventNames = 2048;

    private const int MaximumStackAllocLengthInBytes = 128;

    private readonly string defaultEventNamespace;
    private readonly string defaultEventName;
    private readonly IReadOnlyDictionary<string, EventFullName>? eventFullNameMappings;
    private readonly ResolvedEventFullName defaultEventFullName;

    private int cachedEventNameCount;

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

    internal int CachedEventNameCount => Volatile.Read(ref this.cachedEventNameCount);

    public static bool IsEventNamespaceValid(string eventNamespace)
        => EventNamespaceValidationRegex().IsMatch(eventNamespace);

    public static bool IsEventNameValid(string eventName)
        => EventNameValidationRegex().IsMatch(eventName);

    public static bool IsEventFullNameValid(string eventFullName)
    {
        if (string.IsNullOrEmpty(eventFullName))
        {
            return false;
        }

        foreach (var c in eventFullName)
        {
            if (c is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    public ResolvedEventFullName ResolveEventFullName(
        string eventFullName)
    {
        if (this.EventFullNameCache[eventFullName] is ResolvedEventFullName cachedEventFullName)
        {
            return cachedEventFullName;
        }

        if (!IsEventFullNameValid(eventFullName) ||
            eventFullName.Length is < MinimumEventFullNameLength or > MaximumEventFullNameLength)
        {
            var truncatedEventFullName = eventFullName.Length <= MaximumEventFullNameLength
                ? eventFullName
                : eventFullName.Substring(0, MaximumEventFullNameLength);

            OneCollectorExporterEventSource.Log.EventFullNameDiscarded(string.Empty, truncatedEventFullName);

            return this.defaultEventFullName;
        }

        var resolvedEventFullName = new ResolvedEventFullName(
            BuildEventFullName(string.Empty, eventFullName),
            originalEventNamespace: null,
            originalEventName: null);

        if (this.EventFullNameCache.Count < MaxNumberOfCachedEventFullNames)
        {
            lock (this.EventFullNameCache)
            {
                if (this.EventFullNameCache.Count < MaxNumberOfCachedEventFullNames
                    && this.EventFullNameCache[eventFullName] is null)
                {
                    this.EventFullNameCache[eventFullName] = resolvedEventFullName;
                }
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

        if (eventNameCache?[eventName!] is ResolvedEventFullName cachedEventFullName)
        {
            return cachedEventFullName;
        }

        var eventFullNameBlob = this.ResolveEventNameRare(
            ref eventNamespace!,
            ref eventName!);

        // Note: These are the values supplied by the caller, so they are reported as-is
        // (they are only set when they differ from the resolved value, which is typically
        // because they failed validation). They are written as JSON strings by the
        // serializer so that they are escaped - they must NOT be turned into raw JSON.
        var originalEventNamespaceValue =
            !string.IsNullOrEmpty(originalEventNamespace) && originalEventNamespace != eventNamespace
            ? originalEventNamespace
            : null;

        var originalEventNameValue = !string.IsNullOrEmpty(originalEventName)
                                     && originalEventName != eventName
            ? originalEventName
            : null;

        var resolvedEventFullName = new ResolvedEventFullName(
            eventFullNameBlob,
            originalEventNamespaceValue,
            originalEventNameValue);

        if (eventNameCache != null
            && Volatile.Read(ref this.cachedEventNameCount) < MaxNumberOfCachedEventNames)
        {
            lock (eventNameCache)
            {
                if (eventNameCache[eventName!] is null)
                {
                    eventNameCache[eventName!] = resolvedEventFullName;
                    Interlocked.Increment(ref this.cachedEventNameCount);
                }
            }
        }

        return resolvedEventFullName;
    }

#if NET
    [GeneratedRegex(@"^[A-Za-z](?:\.?[A-Za-z0-9]+?)*$")]
    private static partial Regex EventNamespaceValidationRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*$")]

    private static partial Regex EventNameValidationRegex();
#else

#pragma warning disable SA1201 // A field should not follow a method
    private static readonly Regex EventNamespaceValidationRegexField = new(@"^[A-Za-z](?:\.?[A-Za-z0-9]+?)*$", RegexOptions.Compiled);
    private static readonly Regex EventNameValidationRegexField = new(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);
#pragma warning restore SA1201 // A field should not follow a method

    private static Regex EventNamespaceValidationRegex() => EventNamespaceValidationRegexField;

    private static Regex EventNameValidationRegex() => EventNameValidationRegexField;
#endif

    private static byte[] BuildEventFullName(string eventNamespace, string eventName)
    {
        // The result is written into the payload as raw JSON, so every caller must have
        // validated both components first: they may only contain characters which do not
        // require JSON escaping, and their combined length may not exceed MaximumEventFullNameLength.
        // The buffer is still sized from the input rather than being a fixed size so that
        // an unvalidated caller cannot walk off the end of it.
        Debug.Assert(
            IsEventFullNameValid(eventNamespace.Length > 0 ? $"{eventNamespace}.{eventName}" : eventName),
            "eventNamespace and/or eventName contained characters which require JSON escaping");

        // 2 for the surrounding quotes and 1 for the '.' separator.
        var requiredLength = eventNamespace.Length + eventName.Length + (eventNamespace.Length > 0 ? 1 : 0) + 2;

        Debug.Assert(
            requiredLength <= MaximumEventFullNameLength + 2,
            "eventNamespace and eventName combined exceeded MaximumEventFullNameLength");

        var destination = requiredLength <= MaximumStackAllocLengthInBytes
            ? stackalloc byte[MaximumStackAllocLengthInBytes]
            : new byte[requiredLength];

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

    /// <summary>
    /// Gets the cache of event names for an event namespace, or <see langword="null"/> if the
    /// namespace is not cached and <see cref="MaxNumberOfCachedEventNamespaces"/> has been
    /// reached, in which case the caller has to resolve the event full name every time.
    /// </summary>
    private Hashtable? GetEventNameCacheForEventNamespace(string eventNamespace)
    {
        var eventNamespaceCache = this.EventNamespaceCache;

        if (eventNamespaceCache[eventNamespace] is not Hashtable eventNameCacheForNamespace)
        {
            if (eventNamespaceCache.Count >= MaxNumberOfCachedEventNamespaces)
            {
                return null;
            }

            lock (eventNamespaceCache)
            {
                eventNameCacheForNamespace = (eventNamespaceCache[eventNamespace] as Hashtable)!;
                if (eventNameCacheForNamespace == null)
                {
                    if (eventNamespaceCache.Count >= MaxNumberOfCachedEventNamespaces)
                    {
                        return null;
                    }

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
        public ResolvedEventFullName(
            byte[] eventFullName,
            string? originalEventNamespace,
            string? originalEventName)
        {
            this.EventFullName = eventFullName;
            this.OriginalEventNamespace = originalEventNamespace;
            this.OriginalEventName = originalEventName;
        }

        /// <summary>
        /// Gets the resolved event full name as raw JSON, including the surrounding quotes.
        /// Only ever built from validated components.
        /// </summary>
        public byte[] EventFullName { get; }

        /// <summary>
        /// Gets the unvalidated event namespace supplied by the caller, if it differs from the
        /// resolved one. This has to be written as a JSON string so that it is escaped.
        /// </summary>
        public string? OriginalEventNamespace { get; }

        /// <summary>
        /// Gets the unvalidated event name supplied by the caller, if it differs from the
        /// resolved one. This has to be written as a JSON string so that it is escaped.
        /// </summary>
        public string? OriginalEventName { get; }
    }
}
