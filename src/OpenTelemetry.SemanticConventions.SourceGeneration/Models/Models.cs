// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions.SourceGeneration;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Models;

/// <summary>
/// Resolved registry as embedded into the analyzer assembly. One per shipped semconv version.
/// </summary>
internal readonly record struct RegistryModel(
    EquatableArray<GroupModel> Groups,
    EquatableArray<AttributeModel> Catalog);

/// <summary>
/// A semconv group: a set of attributes sharing a prefix, e.g. "disk", "http.client".
/// <see cref="AttributeRefs"/> stores attribute keys (e.g. "disk.io.direction"), which
/// the emitter resolves against <see cref="RegistryModel.Catalog"/>.
/// </summary>
internal readonly record struct GroupModel(
    string Id,
    string Type,
    string Brief,
    string Note,
    string DisplayName,
    string Extends,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    string AnnotationsJson,
    string LineageJson,
    string Prefix,
    string MetricName,
    string Instrument,
    string Unit,
    RequirementLevelModel MetricRequirementLevel,
    string EventName,
    string SpanKind,
    string SpanNameNote,
    string BodyJson,
    EquatableArray<string> EntityAssociations,
    EquatableArray<string> Events,
    EquatableArray<string> AttributeRefs,
    EquatableArray<GroupAttributeModel> Attributes);

/// <summary>
/// One attribute reference inside a semconv group. Requirement level is
/// contextual: the same catalog attribute can have different requirements in
/// different metrics, events, spans, resources, or shared attribute groups.
/// </summary>
internal readonly record struct GroupAttributeModel(
    string Key,
    AttributeTypeModel Type,
    RequirementLevelModel RequirementLevel,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    string Tag,
    bool SamplingRelevant,
    string Namespace,
    bool Inherited,
    string LineageJson,
    EquatableArray<string> Examples);

/// <summary>
/// One semconv attribute definition. Keys are dotted (e.g. "disk.io.direction"); the emitter
/// projects keys to C# identifiers via PascalCase on the dotted form.
/// </summary>
internal readonly record struct AttributeModel(
    string Key,
    AttributeTypeModel Type,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<string> Examples);

/// <summary>
/// Type of an attribute. Distinguishes primitives, template-typed attributes
/// (e.g. <c>http.request.header.&lt;key&gt;</c>), and enum-valued attributes.
/// </summary>
internal abstract record AttributeTypeModel
{
    public sealed record Primitive(string Name) : AttributeTypeModel;
    public sealed record Template(string Name) : AttributeTypeModel;
    public sealed record EnumType(EquatableArray<EnumMemberModel> Members) : AttributeTypeModel;
}

internal readonly record struct EnumMemberModel(
    string Id,
    string Value,
    string Brief,
    StabilityModel Stability,
    DeprecatedModel? Deprecated);

internal enum StabilityModel
{
    Stable,
    Development,
    Deprecated,
    Alpha,
    Beta,
    ReleaseCandidate
}

internal abstract record DeprecatedModel
{
    public sealed record Renamed(string RenamedTo, string Note) : DeprecatedModel;
    public sealed record Obsoleted(string Note) : DeprecatedModel;
    public sealed record Uncategorized(string Note) : DeprecatedModel;
}

/// <summary>
/// Extracted state from a single attributes-marker application — either
/// <c>[SemanticConventionAttributes("&lt;prefix&gt;")]</c> (stable surface) or
/// <c>[SemanticConventionIncubatingAttributes("&lt;prefix&gt;")]</c> (all-stabilities surface).
/// Identifies the user's partial class, the prefix it requested, and which
/// stability projection the surface emits (<see cref="Extractors.StabilityFilter"/>).
/// </summary>
internal readonly record struct MarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter);
