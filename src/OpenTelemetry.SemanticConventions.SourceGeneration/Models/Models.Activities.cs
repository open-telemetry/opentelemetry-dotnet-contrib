// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions.SourceGeneration;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Models;

/// <summary>
/// Activity-attribute projection of the resolved registry. The activity emitter
/// consumes this for typed <c>System.Diagnostics.Activity</c> extension setters.
/// </summary>
internal readonly record struct ActivityRegistryModel(
    EquatableArray<ActivityAttributeModel> Attributes);

/// <summary>
/// One semconv attribute projected to a setter signature. <c>CSharpParameterType</c>
/// resolves the registry's primitive type to the .NET parameter type (<c>string</c>,
/// <c>long</c>, <c>double</c>, <c>bool</c>, or <c>string[]</c>). For enum-typed
/// attributes a nested values class is emitted alongside the setter so the consumer
/// can discover the catalogued values at the call site.
/// </summary>
internal readonly record struct ActivityAttributeModel(
    string Key,
    string CSharpParameterType,
    bool IsTemplate,
    bool IsEnum,
    EquatableArray<EnumMemberModel> EnumMembers,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<string> Examples,
    EquatableArray<ActivityAttributeContextModel> Contexts);

/// <summary>
/// A contextual reference from a semconv group to an attribute surfaced by an
/// Activity setter. Requirement level is deliberately attached to the context,
/// not the catalog attribute, because upstream semconv requirement levels are
/// signal/group-local facts.
/// </summary>
internal readonly record struct ActivityAttributeContextModel(
    string GroupId,
    string GroupType,
    string Prefix,
    RequirementLevelModel RequirementLevel);

/// <summary>
/// Extracted state from a single meters-marker application — either
/// <c>[SemanticConventionMeters("&lt;prefix&gt;")]</c> (stable surface) or
/// <c>[SemanticConventionIncubatingMeters("&lt;prefix&gt;")]</c> (all-stabilities surface).
/// </summary>
internal readonly record struct MetersMarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter);

/// <summary>
/// Extracted state from a single activities-marker application — either
/// <c>[SemanticConventionActivities("&lt;prefix&gt;")]</c> (stable surface) or
/// <c>[SemanticConventionIncubatingActivities("&lt;prefix&gt;")]</c> (all-stabilities surface).
/// </summary>
internal readonly record struct ActivitiesMarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter);
