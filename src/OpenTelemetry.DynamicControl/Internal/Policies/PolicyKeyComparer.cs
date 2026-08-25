// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Compares <see cref="PolicyKey"/> instances by type then ID, and provides
/// equality and hash-code implementations following the same ordinal rules.
/// </summary>
internal sealed class PolicyKeyComparer : IComparer<PolicyKey>, IEqualityComparer<PolicyKey>
{
    private PolicyKeyComparer()
    {
    }

    /// <summary>
    /// Gets the default instance of <see cref="PolicyKeyComparer"/>.
    /// </summary>
    public static PolicyKeyComparer Default { get; } = new();

    /// <inheritdoc/>
    public int Compare(PolicyKey x, PolicyKey y) => x.CompareTo(y);

    /// <inheritdoc/>
    public bool Equals(PolicyKey x, PolicyKey y) => x.Equals(y);

    /// <inheritdoc/>
    public int GetHashCode(PolicyKey obj) => obj.GetHashCode();
}
