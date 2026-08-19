// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// Describes the transport or backing store that a configured policy source reads from.
/// </summary>
/// <remarks>
/// The kind is diagnostic and grouping information only; it never identifies a source,
/// because several sources of the same kind may be configured.
/// </remarks>
internal enum PolicySourceKind
{
    /// <summary>
    /// The kind is unspecified. This is the <see langword="default"/> value and is not a
    /// valid kind for a configured source.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The source reads policies from the local file system.
    /// </summary>
    File = 1,

    /// <summary>
    /// The source receives policies over OpAMP.
    /// </summary>
    OpAmp = 2,
}
