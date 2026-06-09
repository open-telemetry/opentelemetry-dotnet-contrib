// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
using CustomFieldSet = System.Collections.Frozen.FrozenSet<string>;
using TableLookup = System.Collections.Frozen.FrozenDictionary<string, System.Collections.Frozen.FrozenSet<string>>;
#else
using CustomFieldSet = System.Collections.Generic.HashSet<string>;
using TableLookup = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>;
#endif
using System.Text;

// CustomFieldSet/TableLookup are TFM-specific aliases (Frozen* on .NET, mutable
// collections otherwise). On the non-.NET target an alias resolves to the same
// concrete type used to build the collection, so the analyzer suggests collapsing
// the construction onto the alias. Doing so would break the .NET build where the
// alias is an abstract Frozen type, so name simplification is intentionally
// disabled in this file.
#pragma warning disable IDE0001 // Name can be simplified

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
/// Resolves the set of custom fields that applies to a given physical table.
/// Custom fields can be configured globally (via
/// <see cref="GenevaExporterOptions.CustomFields"/>) and/or per table (via
/// <see cref="GenevaExporterOptions.CustomFieldsMappings"/>).
///
/// Resolution is performed against the <b>final table name</b> a record is
/// routed to (ie., the table produced by
/// <see cref="GenevaExporterOptions.TableNameMappings"/>), not the incoming log
/// category. Multiple categories that map to the same table therefore share the
/// same custom fields.
///
/// The set of tables with a dedicated configuration is fully known at
/// construction time, so the lookup is precomputed into hash tables (no
/// per-record resolution or caching). Callers pass the already-resolved final
/// table name (the exporters compute it anyway when serializing the table name).
/// </summary>
internal sealed class CustomFieldsLookup
{
    private readonly CustomFieldSet? defaultFields;

    // The per-table configuration is stored twice, once keyed by the table name
    // as a string and once keyed by its ASCII bytes, so that each Resolve
    // overload can match without converting between string and bytes on the hot
    // path. Duplicating the (tiny) configuration is intentional: the number of
    // tables with a dedicated mapping is small, so the extra memory is negligible.
    private readonly TableLookup? perTableFieldsByName;
#if NET
    private readonly FrozenDictionary<Utf8TableName, CustomFieldSet>? perTableFieldsByUtf8;
#else
    private readonly Dictionary<Utf8TableName, CustomFieldSet>? perTableFieldsByUtf8;
#endif
#if NET9_0_OR_GREATER
    private readonly FrozenDictionary<Utf8TableName, CustomFieldSet>.AlternateLookup<ReadOnlySpan<byte>> perTableFieldsByUtf8Span;
#endif
    private readonly bool hasConfiguration;

    public CustomFieldsLookup(
        IEnumerable<string>? globalCustomFields,
        IReadOnlyDictionary<string, IEnumerable<string>>? customFieldsMappings)
    {
        var defaultSet = globalCustomFields != null
            ? new HashSet<string>(globalCustomFields, StringComparer.Ordinal)
            : null;

#if NET
        this.defaultFields = defaultSet?.ToFrozenSet(StringComparer.Ordinal);
#else
        this.defaultFields = defaultSet;
#endif

        Dictionary<string, CustomFieldSet>? byName = null;
        Dictionary<Utf8TableName, CustomFieldSet>? byUtf8 = null;
        if (customFieldsMappings != null)
        {
            foreach (var kv in customFieldsMappings)
            {
                var set = new HashSet<string>(kv.Value, StringComparer.Ordinal);
#if NET
                var fields = set.ToFrozenSet(StringComparer.Ordinal);
#else
                var fields = set;
#endif
                byName ??= new Dictionary<string, CustomFieldSet>(StringComparer.Ordinal);
                byName[kv.Key] = fields;

                byUtf8 ??= new Dictionary<Utf8TableName, CustomFieldSet>(Utf8TableNameComparer.Instance);
                byUtf8[new Utf8TableName(Encoding.ASCII.GetBytes(kv.Key))] = fields;
            }
        }

        if (byName != null)
        {
#if NET
            this.perTableFieldsByName = byName.ToFrozenDictionary(StringComparer.Ordinal);
            this.perTableFieldsByUtf8 = byUtf8!.ToFrozenDictionary(Utf8TableNameComparer.Instance);
#else
            this.perTableFieldsByName = byName;
            this.perTableFieldsByUtf8 = byUtf8;
#endif
#if NET9_0_OR_GREATER
            // Ordinal byte-keyed FrozenDictionary supports a span-based alternate
            // lookup, letting the hot path resolve the table name straight from
            // its already-serialized ASCII bytes without any heap allocation.
            this.perTableFieldsByUtf8Span = this.perTableFieldsByUtf8.GetAlternateLookup<ReadOnlySpan<byte>>();
#endif
        }

        this.hasConfiguration = this.defaultFields != null || this.perTableFieldsByName != null;
    }

    /// <summary>
    /// Resolves the custom fields configured for <paramref name="tableName"/>.
    /// </summary>
    /// <param name="tableName">The final (physical) table name.</param>
    /// <returns>
    /// The set of custom field names, or <see langword="null"/> when no custom
    /// fields are configured for the table (in which case all user-defined
    /// fields are made into dedicated fields).
    /// </returns>
    public CustomFieldSet? Resolve(string tableName)
    {
        if (!this.hasConfiguration)
        {
            return null;
        }

        if (this.perTableFieldsByName != null && this.perTableFieldsByName.TryGetValue(tableName, out var fields))
        {
            return fields;
        }

        return this.defaultFields;
    }

    /// <summary>
    /// Resolves the custom fields configured for the table whose name is encoded
    /// in <paramref name="tableNameStr8"/>. This overload avoids decoding the
    /// table name to a heap string on the hot path.
    /// </summary>
    /// <param name="tableNameStr8">
    /// The final (physical) table name as a MessagePack str8 value (a 2-byte
    /// header followed by the ASCII table name).
    /// </param>
    /// <returns>
    /// The set of custom field names, or <see langword="null"/> when no custom
    /// fields are configured for the table.
    /// </returns>
    public CustomFieldSet? Resolve(ReadOnlySpan<byte> tableNameStr8)
    {
        if (!this.hasConfiguration)
        {
            return null;
        }

        if (this.perTableFieldsByUtf8 != null && tableNameStr8.Length > 2)
        {
            // Skip the 2-byte str8 header to get the ASCII table name payload.
            var payload = tableNameStr8.Slice(2);
#if NET9_0_OR_GREATER
            if (this.perTableFieldsByUtf8Span.TryGetValue(payload, out var fields))
            {
                return fields;
            }
#else
            if (this.perTableFieldsByUtf8.TryGetValue(new Utf8TableName(payload.ToArray()), out var fields))
            {
                return fields;
            }
#endif
        }

        return this.defaultFields;
    }

    private readonly struct Utf8TableName
    {
        public readonly byte[] Bytes;

        public Utf8TableName(byte[] bytes)
        {
            this.Bytes = bytes;
        }
    }

    private sealed class Utf8TableNameComparer :
#if NET9_0_OR_GREATER
        IAlternateEqualityComparer<ReadOnlySpan<byte>, Utf8TableName>,
#endif
        IEqualityComparer<Utf8TableName>
    {
        public static readonly Utf8TableNameComparer Instance = new();

        private const uint Fnv1aOffsetBasis = 2166136261;
        private const uint Fnv1aPrime = 16777619;

        public bool Equals(Utf8TableName x, Utf8TableName y)
            => x.Bytes.AsSpan().SequenceEqual(y.Bytes);

        public int GetHashCode(Utf8TableName obj) => Hash(obj.Bytes);

#if NET9_0_OR_GREATER
        public bool Equals(ReadOnlySpan<byte> alternate, Utf8TableName other)
            => alternate.SequenceEqual(other.Bytes);

        public int GetHashCode(ReadOnlySpan<byte> alternate) => Hash(alternate);

        public Utf8TableName Create(ReadOnlySpan<byte> alternate)
            => new(alternate.ToArray());
#endif

        private static int Hash(ReadOnlySpan<byte> bytes)
        {
            var hash = Fnv1aOffsetBasis;
            foreach (var b in bytes)
            {
                hash = (hash ^ b) * Fnv1aPrime;
            }

            return (int)hash;
        }
    }
}
