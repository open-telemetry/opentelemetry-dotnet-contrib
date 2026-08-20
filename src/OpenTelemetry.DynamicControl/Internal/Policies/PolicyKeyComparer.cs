// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Compares <see cref="PolicyKey"/> instances by type then ID.
/// </summary>
internal sealed class PolicyKeyComparer : IComparer<PolicyKey>
{
    // Expressed as an IComparer<PolicyKey> rather than IComparable<PolicyKey> to avoid
    // implying general ordering semantics for policy identity. The comparer expresses
    // "a deterministic order for processing", not "one policy key is less than another".

    private PolicyKeyComparer()
    {
    }

    /// <summary>
    /// Gets the singleton instance of <see cref="PolicyKeyComparer"/>.
    /// </summary>
    public static PolicyKeyComparer Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(PolicyKey x, PolicyKey y)
    {
        var typeComparison = string.CompareOrdinal(x.PolicyType, y.PolicyType);
        return typeComparison != 0 ? typeComparison : string.CompareOrdinal(x.PolicyId, y.PolicyId);
    }
}
