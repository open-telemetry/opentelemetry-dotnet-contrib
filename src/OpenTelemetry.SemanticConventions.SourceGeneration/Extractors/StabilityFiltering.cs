// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions.SourceGeneration;
using OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Selects which semconv stability tiers a marker's generated surface includes.
/// <see cref="StableOnly"/> mirrors the contrib/Java/Python "stable package"
/// projection; <see cref="AllStabilities"/> mirrors the "incubating package"
/// projection (stable + development + deprecated + alpha + beta + release_candidate).
/// </summary>
/// <remarks>
/// Phase A surfaces this enum on every marker model and through the per-signal
/// loader/emitter signatures. Each emitter consumes the filter inside its row
/// selection logic so stable and incubating projections stay consistent across
/// attributes, metrics, events, meters, and activity helpers.
/// </remarks>
internal enum StabilityFilter
{
    /// <summary>Emit only rows whose stability is <see cref="StabilityModel.Stable"/>.</summary>
    StableOnly,

    /// <summary>Emit rows of every stability tier (stable + experimental).</summary>
    AllStabilities
}

internal static class StabilityFiltering
{
    /// <summary>
    /// Returns <c>true</c> when a registry row of the given <paramref name="stability"/>
    /// should be emitted under the requested <paramref name="filter"/>. Surface emitters
    /// call this once per row to gate emission.
    /// </summary>
    public static bool IsIncluded(StabilityModel stability, StabilityFilter filter) =>
        filter == StabilityFilter.AllStabilities || stability == StabilityModel.Stable;

    /// <summary>
    /// Deprecated symbols stay emitted in every projection until upstream removes
    /// them, so consumers can migrate at their own pace. Non-deprecated rows still
    /// follow the requested stability projection.
    /// </summary>
    public static bool IsIncludedOrDeprecated(
        StabilityModel stability,
        DeprecatedModel? deprecated,
        StabilityFilter filter) =>
        IsIncluded(stability, filter) || stability == StabilityModel.Deprecated || deprecated is not null;
}
