// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Tests;

public class AttributesTests
{
    [Fact]
    public void HttpAttributes_StableNames_MatchSpecValues()
    {
        Assert.Equal("http.request.method", HttpAttributes.AttributeHttpRequestMethod);
        Assert.Equal("http.request.method_original", HttpAttributes.AttributeHttpRequestMethodOriginal);
        Assert.Equal("http.response.status_code", HttpAttributes.AttributeHttpResponseStatusCode);
        Assert.Equal("http.route", HttpAttributes.AttributeHttpRoute);
    }

    [Fact]
    public void NetworkAttributes_StableNames_MatchSpecValues()
    {
        Assert.Equal("network.protocol.name", NetworkAttributes.AttributeNetworkProtocolName);
        Assert.Equal("network.protocol.version", NetworkAttributes.AttributeNetworkProtocolVersion);
        Assert.Equal("network.transport", NetworkAttributes.AttributeNetworkTransport);
    }

    [Fact]
    public void ServerClientUrlAttributes_MatchSpecValues()
    {
        Assert.Equal("server.address", ServerAttributes.AttributeServerAddress);
        Assert.Equal("server.port", ServerAttributes.AttributeServerPort);
        Assert.Equal("client.address", ClientAttributes.AttributeClientAddress);
        Assert.Equal("client.port", ClientAttributes.AttributeClientPort);
        Assert.Equal("url.scheme", UrlAttributes.AttributeUrlScheme);
        Assert.Equal("url.full", UrlAttributes.AttributeUrlFull);
    }

    [Fact]
    public void CoreResourceAttributes_MatchSpecValues()
    {
        Assert.Equal("service.name", ServiceAttributes.AttributeServiceName);
        Assert.Equal("service.instance.id", ServiceAttributes.AttributeServiceInstanceId);
        Assert.Equal("telemetry.sdk.name", TelemetryAttributes.AttributeTelemetrySdkName);
        Assert.Equal("telemetry.sdk.language", TelemetryAttributes.AttributeTelemetrySdkLanguage);
        Assert.Equal("telemetry.sdk.version", TelemetryAttributes.AttributeTelemetrySdkVersion);
    }

    [Fact]
    public void ExceptionAttributes_MatchSpecValues()
    {
        Assert.Equal("exception.message", ExceptionAttributes.AttributeExceptionMessage);
        Assert.Equal("exception.stacktrace", ExceptionAttributes.AttributeExceptionStacktrace);
        Assert.Equal("exception.type", ExceptionAttributes.AttributeExceptionType);
    }

    [Fact]
    public void HttpRequestMethodValues_ContainStandardVerbs()
    {
        Assert.Equal("CONNECT", HttpAttributes.HttpRequestMethodValues.Connect);
        Assert.Equal("DELETE", HttpAttributes.HttpRequestMethodValues.Delete);
        Assert.Equal("GET", HttpAttributes.HttpRequestMethodValues.Get);
        Assert.Equal("HEAD", HttpAttributes.HttpRequestMethodValues.Head);
        Assert.Equal("OPTIONS", HttpAttributes.HttpRequestMethodValues.Options);
        Assert.Equal("PATCH", HttpAttributes.HttpRequestMethodValues.Patch);
        Assert.Equal("POST", HttpAttributes.HttpRequestMethodValues.Post);
        Assert.Equal("PUT", HttpAttributes.HttpRequestMethodValues.Put);
        Assert.Equal("TRACE", HttpAttributes.HttpRequestMethodValues.Trace);
    }

    [Theory]
    [InlineData(typeof(HttpAttributes), "AttributeHttpFlavor")]
    [InlineData(typeof(HttpAttributes), "AttributeHttpMethod")]
    [InlineData(typeof(HttpAttributes), "AttributeHttpHost")]
    [InlineData(typeof(NetAttributes), "AttributeNetSockHostAddr")]
    public void DeprecatedAttribute_IsMarkedObsolete(System.Type containingType, string fieldName)
    {
        var field = containingType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.NotEmpty(field!.GetCustomAttributes(typeof(System.ObsoleteAttribute), inherit: false));
    }

    [Fact]
    public void AttributeConstants_FollowAttributePrefixConvention()
    {
        // Every public string constant emitted into an *Attributes class should follow
        // the "Attribute" prefix naming convention (e.g. AttributeHttpRequestMethod).
        var attributeClass = typeof(HttpAttributes);
        var publicConstants = attributeClass
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToArray();

        Assert.NotEmpty(publicConstants);
        Assert.All(publicConstants, f => Assert.StartsWith("Attribute", f.Name, System.StringComparison.Ordinal));
    }
}
