// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// Describes why a policy payload value was rejected.
/// </summary>
internal enum PolicyRejectionReason
{
    /// <summary>
    /// The value was not rejected.
    /// </summary>
    None = 0,

    /// <summary>
    /// The entry was not in a shape the payload format accepts, such as an unusable value
    /// kind or a missing required member.
    /// </summary>
    InvalidPayloadShape = 1,

    /// <summary>
    /// The entry had an accepted shape, but its value could not be parsed or validated.
    /// </summary>
    InvalidPolicyValue = 2,

    /// <summary>
    /// The payload declares a recognized key more than once.
    /// All entries for that key are rejected.
    /// </summary>
    DuplicateKey = 3,
}
