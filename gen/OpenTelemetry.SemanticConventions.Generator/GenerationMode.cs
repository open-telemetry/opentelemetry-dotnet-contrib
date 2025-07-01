// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.SemanticConventions.Generator;

internal enum GenerationMode
{
    /// <summary>
    /// Generate a class with attribute names.
    /// </summary>
    AttributeNames,

    /// <summary>
    /// Generate a class with attribute values.
    /// </summary>
    AttributeValues,
}
