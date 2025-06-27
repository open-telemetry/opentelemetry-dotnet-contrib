// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.SemanticConventions.Example.Target;

internal partial struct TargetDiskAttributeNames
{
    #pragma warning disable SA1300
    #pragma warning disable SA1303
    #pragma warning disable CS8981
    internal partial struct disk
    {
        internal partial struct io
        {
            /// <summary>
            /// The disk IO operation direction.
            /// </summary>
            /// <example>read</example>
            internal const string direction = "disk.io.direction";
        }

        internal partial struct io
        {
            internal const string speed = "disk.io.speed";
        }

        internal partial struct form
        {
            internal const string speed = "disk.form.speed";
        }
    }
    #pragma warning restore CS8981
    #pragma warning restore SA1303
    #pragma warning restore SA1300
}
