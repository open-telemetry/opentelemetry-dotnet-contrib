// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// <auto-generated>This file has been auto generated from 'src\OpenTelemetry.SemanticConventions\scripts\templates\registry\SemanticConventionsAttributes.cs.j2' </auto-generated>

#pragma warning disable CS1570 // XML comment has badly formed XML

using System;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
public static class DiskAttributes
{
    /// <summary>
    /// The disk IO operation direction
    /// </summary>
    public const string AttributeDiskIoDirection = "disk.io.direction";

    /// <summary>
    /// The disk IO operation direction
    /// </summary>
    public static class DiskIoDirectionValues
    {
        /// <summary>
        /// read
        /// </summary>
        public const string Read = "read";

        /// <summary>
        /// write
        /// </summary>
        public const string Write = "write";
    }
}
