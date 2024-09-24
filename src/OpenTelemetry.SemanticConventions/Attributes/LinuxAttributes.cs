// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#nullable enable

#pragma warning disable CS1570 // XML comment has badly formed XML

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class LinuxAttributes
{
    /// <summary>
    /// The Linux Slab memory state.
    /// </summary>
    public const string AttributeLinuxMemorySlabState = "linux.memory.slab.state";

    /// <summary>
    /// The Linux Slab memory state.
    /// </summary>
    public static class LinuxMemorySlabStateValues
    {
        /// <summary>
        /// reclaimable.
        /// </summary>
        public const string Reclaimable = "reclaimable";

        /// <summary>
        /// unreclaimable.
        /// </summary>
        public const string Unreclaimable = "unreclaimable";
    }
}
