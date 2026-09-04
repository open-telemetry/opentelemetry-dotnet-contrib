// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Providers;

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
    /// The entry does not conform to the structure the payload format expects,
    /// such as an unusable value kind or a missing required member.
    /// </summary>
    SchemaMismatch = 1,

    /// <summary>
    /// The entry had the expected structure, but its value could not be parsed or validated.
    /// </summary>
    InvalidValue = 2,

    /// <summary>
    /// The payload declares a recognized key more than once.
    /// All entries for that key are rejected.
    /// </summary>
    DuplicateKey = 3,
}
