// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Describes the transport or backing store that a configured policy provider reads from.
/// </summary>
/// <remarks>
/// The kind is diagnostic and grouping information only; it never identifies a provider,
/// because several providers of the same kind may be configured.
/// </remarks>
internal enum PolicyProviderKind
{
    /// <summary>
    /// The kind is unspecified. This is not a valid kind for a configured provider.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The provider receives policies over OpAMP.
    /// </summary>
    OpAmp = 1,

    /// <summary>
    /// The provider receives policies over HTTP.
    /// </summary>
    Http = 2,

    /// <summary>
    /// The provider reads policies from the local file system.
    /// </summary>
    File = 3,

    /// <summary>
    /// The provider is a user-defined or third-party implementation not covered by the
    /// other named kinds.
    /// </summary>
    Custom = 4,
}
