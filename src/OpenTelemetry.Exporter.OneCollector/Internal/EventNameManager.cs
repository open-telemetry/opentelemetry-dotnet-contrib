// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class EventNameManager
{
    private const int MinimumEventFullNameLength = 4;
    private const int MaximumEventFullNameLength = 100;
    private static readonly Regex EventNamespaceValidationRegex = new(@"^[A-Za-z](?:\.?[A-Za-z0-9]+?)*$", RegexOptions.Compiled);
    private static readonly Regex EventNameValidationRegex = new(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private readonly byte[] defaultEventFullName;
    private readonly Hashtable eventNamespaceCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly OneCollectorLogExporterOptions exporterOptions;

    public EventNameManager(OneCollectorLogExporterOptions exporterOptions)
    {
        Guard.ThrowIfNull(exporterOptions, nameof(exporterOptions));

        Debug.Assert(exporterOptions.DefaultEventNamespace != null, "defaultEventNamespace was null");
        Debug.Assert(exporterOptions.DefaultEventName != null, "defaultEventName was null");

        this.exporterOptions = exporterOptions;

        if (!IsEventNamespaceValid(exporterOptions.DefaultEventNamespace!))
        {
            throw new ArgumentException($"Default event namespace '{exporterOptions.DefaultEventNamespace}' was invalid.", nameof(exporterOptions));
        }

        if (!IsEventNamespaceValid(exporterOptions.DefaultEventName!))
        {
            throw new ArgumentException($"Default event name '{exporterOptions.DefaultEventName}' was invalid.", nameof(exporterOptions));
        }

        var defaultEventFullNameLength = exporterOptions.DefaultEventNamespace!.Length + exporterOptions.DefaultEventName!.Length + 1;
        if (defaultEventFullNameLength < MinimumEventFullNameLength || defaultEventFullNameLength > MaximumEventFullNameLength)
        {
            throw new ArgumentException($"Default event full name '{exporterOptions.DefaultEventNamespace}.{exporterOptions.DefaultEventName}' does not meet length requirements.", nameof(exporterOptions));
        }

        this.defaultEventFullName = BuildEventFullName(exporterOptions.DefaultEventNamespace, exporterOptions.DefaultEventName)!;

#if NET
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

            eventNamespace = this.exporterOptions.DefaultEventNamespace;
        }

        if (eventNameIsNullOrWhiteSpace)
        {
            eventName = this.exporterOptions.DefaultEventName;
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

    private static byte[] BuildEventFullNameBasedOnNamespace(string eventNamespace)
    {
        Span<byte> destination = stackalloc byte[128];

        destination[0] = (byte)'\"';

        var cursor = 1;

        WriteEventFullNameComponent(eventNamespace, destination, ref cursor);

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
        var tableMappingOptions = this.exporterOptions.TableMappingOptions;
        if (tableMappingOptions.UseTableMapping)
        {
            if (tableMappingOptions.TableMappings.TryGetValue(eventNamespace, out var tableMapping))
            {
                eventNamespace = tableMapping;
            }
            else
            {
                // Exact match not found, try to find a partial match.
                KeyValuePair<string, string>? currentEntry = null;
                foreach (var mapping in tableMappingOptions.TableMappings)
                {
                    if (!eventNamespace.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!currentEntry.HasValue || mapping.Key.Length >= currentEntry.Value.Key.Length)
                    {
                        currentEntry = mapping;
                    }
                }

                if (currentEntry.HasValue)
                {
                    eventNamespace = currentEntry.Value.Value;
                }
                else
                {
                    var catchAllFound = tableMappingOptions.TableMappings.TryGetValue(OneCollectorLogExporterTableMappingOptions.CatchAllNamespace, out var defaultTableName);

                    if (catchAllFound && defaultTableName is not null)
                    {
                        eventNamespace = defaultTableName;
                    }
                    else
                    {
                        eventNamespace = OneCollectorLogExporterTableMappingOptions.DefaultTableName;
                    }
                }
            }

            eventName = string.Empty;
        }

        if (!IsEventNamespaceValid(eventNamespace))
        {
            OneCollectorExporterEventSource.Log.EventNamespaceInvalid(eventNamespace);
            eventNamespace = this.exporterOptions.DefaultEventNamespace;
        }

        var eventNameHashtableKey = eventName;

        if (!IsEventNameValid(eventName))
        {
            OneCollectorExporterEventSource.Log.EventNameInvalid(eventName);
            eventName = this.exporterOptions.DefaultEventName;
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
            if (tableMappingOptions.UseTableMapping)
            {
                eventFullName = BuildEventFullNameBasedOnNamespace(eventNamespace!);
            }
            else
            {
                eventFullName = BuildEventFullName(eventNamespace!, eventName!);
            }
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
