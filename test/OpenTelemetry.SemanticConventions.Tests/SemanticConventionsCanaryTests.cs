// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit;

namespace OpenTelemetry.SemanticConventions.Tests;

public class SemanticConventionsCanaryTests
{
    [Fact]
    public void SchemaConstantsMatchPinnedSemanticConventionVersion()
    {
        Assert.Equal("https://opentelemetry.io/schemas/1.41.0", global::OpenTelemetry.SemanticConventions.SchemaUrl.Value);
        Assert.Equal("1.41.0", global::OpenTelemetry.SemanticConventions.SchemaVersion.Value);

        Assert.Equal("https://opentelemetry.io/schemas/1.41.0", global::OpenTelemetry.SemanticConventions.Incubating.SchemaUrl.Value);
        Assert.Equal("1.41.0", global::OpenTelemetry.SemanticConventions.Incubating.SchemaVersion.Value);
    }

    [Fact]
    public void ReservedStableAttributesAreGeneratedInStablePackage()
    {
        Assert.Equal("error.type", global::OpenTelemetry.SemanticConventions.ErrorAttributes.AttributeErrorType);
        Assert.Equal("exception.message", global::OpenTelemetry.SemanticConventions.ExceptionAttributes.AttributeExceptionMessage);
        Assert.Equal("exception.stacktrace", global::OpenTelemetry.SemanticConventions.ExceptionAttributes.AttributeExceptionStacktrace);
        Assert.Equal("service.name", global::OpenTelemetry.SemanticConventions.ServiceAttributes.AttributeServiceName);
        Assert.Equal("server.address", global::OpenTelemetry.SemanticConventions.ServerAttributes.AttributeServerAddress);
        Assert.Equal("server.port", global::OpenTelemetry.SemanticConventions.ServerAttributes.AttributeServerPort);
        Assert.Equal("url.scheme", global::OpenTelemetry.SemanticConventions.UrlAttributes.AttributeUrlScheme);
    }

    [Fact]
    public void Version141IndicatorsAreGeneratedInIncubatingPackage()
    {
        Assert.Equal("gen_ai.tool.name", global::OpenTelemetry.SemanticConventions.Incubating.GenAiAttributes.AttributeGenAiToolName);
        Assert.Equal("process.executable.build_id.htlhash", global::OpenTelemetry.SemanticConventions.Incubating.ProcessAttributes.AttributeProcessExecutableBuildIdHtlhash);
        Assert.Equal("process.pid", global::OpenTelemetry.SemanticConventions.Incubating.ProcessAttributes.AttributeProcessPid);
        Assert.Equal("process.creation.time", global::OpenTelemetry.SemanticConventions.Incubating.ProcessAttributes.AttributeProcessCreationTime);
    }

    [Fact]
    public void GraphqlDocumentIsIncubatingOnly()
    {
        Assert.Equal("graphql.document", global::OpenTelemetry.SemanticConventions.Incubating.GraphqlAttributes.AttributeGraphqlDocument);

        AssertPublicConstantAbsent(
            typeof(global::OpenTelemetry.SemanticConventions.SchemaVersion).Assembly,
            "OpenTelemetry.SemanticConventions.GraphqlAttributes",
            "AttributeGraphqlDocument");
    }

    [Fact]
    public void StableAttributesAreExcludedFromIncubatingPackage()
    {
        AssertPublicConstantAbsent(
            typeof(global::OpenTelemetry.SemanticConventions.Incubating.SchemaVersion).Assembly,
            "OpenTelemetry.SemanticConventions.Incubating.ServiceAttributes",
            "AttributeServiceName");
    }

    private static void AssertPublicConstantAbsent(Assembly assembly, string typeName, string fieldName)
    {
        var type = assembly.GetType(typeName);
        var field = type?.GetField(fieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        Assert.Null(field);
    }
}
