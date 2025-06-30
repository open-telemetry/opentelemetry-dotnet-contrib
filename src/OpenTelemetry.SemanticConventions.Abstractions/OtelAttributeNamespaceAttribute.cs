// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.SemanticConventions;

/// <summary>
/// This attribute is used to trigger the generation of the OpenTelemetry namespace for which you wish to use the attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class OtelAttributeNamespaceAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OtelAttributeNamespaceAttribute"/> class.
    /// </summary>
    public OtelAttributeNamespaceAttribute()
    {
        // Default constructor for cases where no namespace is provided
        this.AttributeNamespace = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtelAttributeNamespaceAttribute"/> class.
    /// </summary>
    /// <param name="attributeNamespace">The namespace which you wish to generate attributes for.</param>
    public OtelAttributeNamespaceAttribute(string attributeNamespace)
    {
        if (string.IsNullOrWhiteSpace(attributeNamespace))
        {
            throw new ArgumentException("Attribute namespace cannot be null or whitespace.", nameof(attributeNamespace));
        }

        this.AttributeNamespace = attributeNamespace;
    }

    /// <summary>
    /// Gets or sets the namespace associated with the attribute.
    /// </summary>
    [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments", Justification = "We want them to be public due to being attributes.")]
    public string AttributeNamespace { get; set; }
}
