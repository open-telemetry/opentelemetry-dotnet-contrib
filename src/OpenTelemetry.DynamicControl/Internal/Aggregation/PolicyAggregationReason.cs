// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Aggregation;

/// <summary>
/// Describes why a retained policy is not the effective policy for its key.
/// </summary>
internal enum PolicyAggregationReason
{
    /// <summary>
    /// Default/uninitialized value. This member should not appear in production paths.
    /// </summary>
    None = 0,

    /// <summary>
    /// A provider of higher precedence supplied the same key. The policy stays retained and
    /// becomes effective if the providers ahead of it stop supplying the key.
    /// </summary>
    Superseded = 1,

    /// <summary>
    /// A provider of equal precedence supplied the same key and won the tie-break on
    /// registration identity. The policy stays retained.
    /// </summary>
    Conflicting = 2,
}
