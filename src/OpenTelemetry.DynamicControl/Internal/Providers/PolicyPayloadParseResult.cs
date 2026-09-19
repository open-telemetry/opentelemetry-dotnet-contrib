// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Describes the outcome of decoding one complete policy payload.
/// </summary>
/// <remarks>
/// <para>
/// A payload either fails as a whole or decodes into a complete policy set. Failure of the
/// payload itself is reported by <see cref="IsMalformed"/>; failure of an individual entry
/// is reported through <see cref="Rejections"/> and leaves the remaining entries usable.
/// </para>
/// <para>
/// A decoded result with no policies is a meaningful outcome rather than an absence of one:
/// it states that the payload carries no policies. <see cref="Rejections"/> distinguishes a
/// payload that carried nothing recognizable from one whose every entry was unusable.
/// </para>
/// </remarks>
internal sealed class PolicyPayloadParseResult
{
    private PolicyPayloadParseResult(
        bool isMalformed,
        string? error,
        ImmutableArray<TelemetryPolicy> policies,
        ImmutableArray<PolicyPayloadRejection> rejections,
        ImmutableArray<string> ignoredKeys)
    {
        this.IsMalformed = isMalformed;
        this.Error = error;
        this.Policies = policies;
        this.Rejections = rejections;
        this.IgnoredKeys = ignoredKeys;
    }

    /// <summary>
    /// Gets a value indicating whether the payload could not be decoded at all.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, nothing about the payload's intended content is known,
    /// so <see cref="Policies"/> must not be treated as a replacement set.
    /// </remarks>
    public bool IsMalformed { get; }

    /// <summary>
    /// Gets a description of why the payload could not be decoded, or <see langword="null"/>
    /// when it was decoded successfully.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the policies the payload declared, in the order they appeared.
    /// </summary>
    /// <remarks>
    /// When the payload was decoded, this is the complete set it declared, never a delta.
    /// </remarks>
    public ImmutableArray<TelemetryPolicy> Policies { get; }

    /// <summary>
    /// Gets the entries that were decoded but could not be used, in the order they appeared.
    /// </summary>
    public ImmutableArray<PolicyPayloadRejection> Rejections { get; }

    /// <summary>
    /// Gets the distinct keys the payload carried that this package does not recognize, in
    /// the order they first appeared.
    /// </summary>
    /// <remarks>
    /// An unrecognized key is not a failure. A payload shared across several SDKs routinely
    /// carries keys this package has no policy type for.
    /// </remarks>
    public ImmutableArray<string> IgnoredKeys { get; }

    /// <summary>
    /// Creates a result stating that the payload could not be decoded.
    /// </summary>
    /// <param name="error">A description of why the payload could not be decoded. Must not be null or whitespace.</param>
    /// <returns>A malformed result carrying no policies, rejections, or ignored keys.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="error"/> is null, empty, or whitespace.
    /// </exception>
    public static PolicyPayloadParseResult Malformed(string error)
    {
        Guard.ThrowIfNullOrWhitespace(error);

        return new(true, error, [], [], []);
    }

    /// <summary>
    /// Creates a result carrying the complete policy set a payload declared.
    /// </summary>
    /// <param name="policies">The policies the payload declared, if any.</param>
    /// <param name="rejections">The entries that could not be used, if any.</param>
    /// <param name="ignoredKeys">The distinct unrecognized keys the payload carried, if any.</param>
    /// <returns>A decoded result.</returns>
    public static PolicyPayloadParseResult Decoded(
        ImmutableArray<TelemetryPolicy> policies,
        ImmutableArray<PolicyPayloadRejection> rejections,
        ImmutableArray<string> ignoredKeys) =>
            new(false, null, policies, rejections, ignoredKeys);
}
