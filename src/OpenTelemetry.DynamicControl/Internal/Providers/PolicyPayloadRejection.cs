// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Records one entry of a decoded policy payload that could not be turned into a policy.
/// </summary>
/// <remarks>
/// A rejection never prevents sibling entries from being decoded. It exists so that a
/// caller can report why an entry was dropped without inspecting the payload again.
/// </remarks>
internal sealed class PolicyPayloadRejection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyPayloadRejection"/> class.
    /// </summary>
    /// <param name="location">Where in the payload the rejected entry appeared.</param>
    /// <param name="reason">The category of failure.</param>
    /// <param name="message">A description of the failure. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="message"/> is null, empty, or whitespace.
    /// </exception>
    public PolicyPayloadRejection(PayloadEntryLocation location, PolicyRejectionReason reason, string message)
    {
        Guard.ThrowIfNullOrWhitespace(message);

        this.Location = location;
        this.Reason = reason;
        this.Message = message;
    }

    /// <summary>
    /// Gets the location in the payload of the rejected entry.
    /// </summary>
    public PayloadEntryLocation Location { get; }

    /// <summary>
    /// Gets the category of failure.
    /// </summary>
    public PolicyRejectionReason Reason { get; }

    /// <summary>
    /// Gets a description of the failure.
    /// </summary>
    public string Message { get; }
}
