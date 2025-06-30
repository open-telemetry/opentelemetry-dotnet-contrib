// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.SemanticConventions.Generator;

internal enum GenerationMode
{
    /// <summary>
    /// Generate a class with attributes.
    /// </summary>
    AttributeNamesAndValues,

    /// <summary>
    /// Generate a class with enums.
    /// </summary>
    AttributeNames,

    /// <summary>
    /// Generate a class with attributes and enums.
    /// </summary>
    AttributeValues,
}
