// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Cloud.Trace.V2;
using OpenTelemetry.Exporter.Stackdriver.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.Stackdriver.Tests;

public class ResourceExtensionsTests
{
    [Fact]
    public void Enriches_Span_Attributes_With_Service_Name()
    {
        const string serviceName = "some service name";
        const string serviceVersion = "2.3.4";
        var resource = new Resource(new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = "value2",
            [ResourceSemanticConventions.AttributeServiceName] = serviceName,
            [ResourceSemanticConventions.AttributeServiceVersion] = serviceVersion,
        });

        var span = new Span();
        span.AnnotateWith(resource);
        Assert.Contains(span.Attributes.AttributeMap, kvp =>
            kvp.Key == ResourceSemanticConventions.AttributeServiceName &&
            kvp.Value.StringValue.Value == serviceName);

        Assert.Contains(span.Attributes.AttributeMap, kvp =>
            kvp.Key == ResourceSemanticConventions.AttributeServiceVersion &&
            kvp.Value.StringValue.Value == serviceVersion);
    }

    [Fact]
    public void Otel_Resource_Has_No_Service_Name()
    {
        var resource = new Resource(new Dictionary<string, object> { ["key1"] = "value1", ["key2"] = "value2" });

        var span = new Span();
        span.AnnotateWith(resource);
    }

    [Fact]
    public void Enriches_Span_Attributes_When_Attribute_Already_In_Span()
    {
        var span = new Span { Attributes = new Span.Types.Attributes() };
        span.Attributes.AttributeMap.Add(ResourceSemanticConventions.AttributeServiceName, "world".ToAttributeValue());
        var resource = new Resource(new Dictionary<string, object> { [ResourceSemanticConventions.AttributeServiceName] = "world" });
        span.AnnotateWith(resource);
    }
}
