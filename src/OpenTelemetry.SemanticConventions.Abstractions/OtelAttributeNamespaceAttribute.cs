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
        this.Namespace = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtelAttributeNamespaceAttribute"/> class.
    /// </summary>
    /// <param name="otelNamespace">The namespace which you wish to generate attributes for.</param>
    [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments", Justification = "This field is mapped to Namespace")]
    public OtelAttributeNamespaceAttribute(string otelNamespace)
    {
        if (string.IsNullOrWhiteSpace(otelNamespace))
        {
            throw new ArgumentException("Attribute namespace cannot be null or whitespace.", nameof(otelNamespace));
        }

        this.Namespace = otelNamespace;
    }

    /// <summary>
    /// Gets or sets the namespace associated with the attribute.
    /// </summary>
    [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments", Justification = "We want them to be public due to being attributes.")]
    public string Namespace { get; set; }
}
