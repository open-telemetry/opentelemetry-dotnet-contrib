// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// The outcome of looking for a single named member of a JSON object.
/// </summary>
internal enum JsonMemberLookup
{
    /// <summary>
    /// No lookup outcome has been assigned.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The object declares the member exactly once.
    /// </summary>
    Found = 1,

    /// <summary>
    /// The object does not declare the member.
    /// </summary>
    Missing = 2,

    /// <summary>
    /// The object declares the member more than once.
    /// </summary>
    Repeated = 3,
}
