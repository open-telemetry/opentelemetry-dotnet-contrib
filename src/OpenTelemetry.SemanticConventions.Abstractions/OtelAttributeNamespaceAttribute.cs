// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.SemanticConventions;

[AttributeUsage(AttributeTargets.Struct)]
public class OtelAttributeNamespaceAttribute : Attribute
{
    public OtelAttributeNamespaceAttribute()
    {
        // Default constructor for cases where no namespace is provided
        AttributeNamespace = string.Empty;
    }

    public OtelAttributeNamespaceAttribute(string attributeNamespace)
    {
        if (string.IsNullOrWhiteSpace(attributeNamespace))
        {
            throw new ArgumentException("Attribute namespace cannot be null or whitespace.", nameof(attributeNamespace));
        }

        AttributeNamespace = attributeNamespace;
    }

    public string AttributeNamespace { get; set; }
}
