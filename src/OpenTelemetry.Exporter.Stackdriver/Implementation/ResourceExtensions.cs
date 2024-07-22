// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Cloud.Trace.V2;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Stackdriver.Implementation;

internal static class ResourceExtensions
{
    private static readonly HashSet<string> ExportableResourceNames =
    [
        ResourceSemanticConventions.AttributeServiceName,
        ResourceSemanticConventions.AttributeServiceVersion
    ];

    /// <summary>
    ///     Adds resource attributes to the span.
    /// </summary>
    /// <param name="span">Google Cloud Trace Span to be annotated.</param>
    /// <param name="resource">
    ///     The Resource contains attributes such as "service.name" that provide metadata about the service being traced.
    ///     These attributes are used to annotate the Google Cloud Trace Span, enhancing the trace data available in the Google
    ///     Trace Explorer UI.
    /// </param>
    public static void AnnotateWith(this Span span, Resource resource)
    {
        span.Attributes ??= new Span.Types.Attributes();
        foreach (var attr in resource.Attributes)
        {
            var attributeMap = span.Attributes.AttributeMap;
            if (ExportableResourceNames.Contains(attr.Key) && !attributeMap.ContainsKey(attr.Key))
            {
                attributeMap.Add(attr.Key, attr.Value.ToAttributeValue());
            }
        }
    }
}
