// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions.SourceGeneration;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Models;

/// <summary>
/// Metric and event projection of the embedded Weaver-derived registry. Metrics and
/// meter factories intentionally share <see cref="MetricDescriptorModel"/> so both
/// surfaces preserve the same name, instrument, unit, attributes, examples, and
/// entity-association facts.
/// </summary>
internal readonly record struct InstrumentRegistryModel(
    EquatableArray<MetricDescriptorModel> Metrics,
    EquatableArray<EventGroupModel> Events);

/// <summary>
/// A semconv metric group (a registry entry with <c>type == "metric"</c>).
/// </summary>
internal readonly record struct MetricDescriptorModel(
    string MetricName,
    string Instrument,
    string Unit,
    RequirementLevelModel MetricRequirementLevel,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<string> AttributeRefs,
    EquatableArray<SignalAttributeModel> Attributes,
    EquatableArray<string> EntityAssociations);

/// <summary>
/// A semconv event group (a registry entry with <c>type == "event"</c>).
/// </summary>
internal readonly record struct EventGroupModel(
    string EventName,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EventEmissionTargetModel EmissionTarget,
    string BodyJson,
    EquatableArray<string> EntityAssociations,
    EquatableArray<SignalAttributeModel> Payload);

/// <summary>
/// Event target information when upstream exposes it. The semconv v1.41.0
/// registry projection does not carry a discriminator, so generated events
/// remain target-agnostic instead of pretending every event is an ActivityEvent.
/// </summary>
internal enum EventEmissionTargetModel
{
    Unspecified,
    LogRecord,
    ActivityEvent
}

/// <summary>
/// One signal-specific attribute reference, preserving the upstream requirement
/// level and any local brief/note/examples override supplied on the signal.
/// </summary>
internal readonly record struct SignalAttributeModel(
    string Key,
    AttributeTypeModel Type,
    RequirementLevelModel RequirementLevel,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<string> Examples);

internal readonly record struct RequirementLevelModel(
    RequirementLevelKind Kind,
    string Condition);

internal enum RequirementLevelKind
{
    Unspecified,
    Required,
    Recommended,
    OptIn,
    ConditionallyRequired
}

/// <summary>
/// Extracted state from a single metrics-marker application — either
/// <c>[SemanticConventionMetrics("&lt;prefix&gt;")]</c> (stable surface) or
/// <c>[SemanticConventionIncubatingMetrics("&lt;prefix&gt;")]</c> (all-stabilities surface).
/// Mirrors <see cref="MarkerModel"/> for the metrics generator.
/// </summary>
internal readonly record struct MetricsMarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter);

/// <summary>
/// Extracted state from a single events-marker application — either
/// <c>[SemanticConventionEvents("&lt;prefix&gt;")]</c> (stable surface) or
/// <c>[SemanticConventionIncubatingEvents("&lt;prefix&gt;")]</c> (all-stabilities surface).
/// Mirrors <see cref="MarkerModel"/> for the events generator.
/// </summary>
internal readonly record struct EventsMarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter);
