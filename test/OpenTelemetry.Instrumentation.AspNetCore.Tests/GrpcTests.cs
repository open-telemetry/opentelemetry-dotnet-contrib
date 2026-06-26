// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

public class GrpcTests
{
    [Fact]
    public void OnStopActivityAddsGrpcAttributesForParsedMethodAndValidStatusCode()
    {
        // Arrange
        var listener = CreateListener();
        using var activity = CreateActivity("/package.Service/Method", "13");
        var context = CreateContext(IPAddress.Loopback, remotePort: 4317);

        // Act
        listener.OnStopActivity(activity, context);

        // Assert
        Assert.Equal("package.Service/Method", activity.DisplayName);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);

        AssertTag(activity, GrpcTagHelper.GrpcMethodTagName, null);
        AssertTag(activity, GrpcTagHelper.GrpcStatusCodeTagName, null);
        AssertTag(activity, SemanticConventions.AttributeHttpResponseStatusCode, 200);

        AssertTag(activity, SemanticConventions.AttributeRpcSystemName, "grpc");
        AssertTag(activity, SemanticConventions.AttributeNetworkPeerAddress, "127.0.0.1");
        AssertTag(activity, SemanticConventions.AttributeNetworkPeerPort, 4317);
        AssertTag(activity, SemanticConventions.AttributeRpcMethod, "package.Service/Method");
        AssertTag(activity, SemanticConventions.AttributeRpcMethodOriginal, null);
        AssertTag(activity, SemanticConventions.AttributeRpcResponseStatusCode, "INTERNAL");
        AssertTag(activity, SemanticConventions.AttributeErrorType, "INTERNAL");
    }

    [Fact]
    public void OnStopActivityIgnoresEmptyGrpcMethodTag()
    {
        // Arrange
        var listener = CreateListener();
        using var activity = CreateActivity(string.Empty, "13");
        var context = CreateContext(IPAddress.Loopback, remotePort: 4317);

        // Act
        listener.OnStopActivity(activity, context);

        // Assert
        Assert.Equal("operation", activity.DisplayName);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);

        AssertTag(activity, GrpcTagHelper.GrpcMethodTagName, string.Empty);
        AssertTag(activity, GrpcTagHelper.GrpcStatusCodeTagName, "13");
        AssertTag(activity, SemanticConventions.AttributeRpcGrpcStatusCode, null);
        AssertTag(activity, SemanticConventions.AttributeRpcSystem, null);
    }

    [Fact]
    public void OnStopActivityDoesNotAddErrorTypeForSuccessfulGrpcStatusCode()
    {
        // Arrange
        var listener = CreateListener();
        using var activity = CreateActivity("/package.Service/Method", "0");
        var context = CreateContext(IPAddress.Loopback, remotePort: 4317);

        // Act
        listener.OnStopActivity(activity, context);

        // Assert
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);

        AssertTag(activity, SemanticConventions.AttributeRpcResponseStatusCode, "OK");
        AssertTag(activity, SemanticConventions.AttributeErrorType, null);
    }

    private static void AssertTag(Activity activity, string name, object? expected) =>
        Assert.Equal(expected, activity.GetTagValue(name));

    private static HttpInListener CreateListener() =>
        new(new AspNetCoreTraceInstrumentationOptions
        {
            EnableGrpcAspNetCoreSupport = true,
        });

    private static Activity CreateActivity(string grpcMethod, string grpcStatusCode)
    {
        var activity = new Activity("operation")
        {
            IsAllDataRequested = true,
        };

        activity.SetTag(GrpcTagHelper.GrpcMethodTagName, grpcMethod);
        activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, grpcStatusCode);

        return activity;
    }

    private static DefaultHttpContext CreateContext(IPAddress? remoteIpAddress, int remotePort)
    {
        var context = new DefaultHttpContext();

        context.Connection.RemoteIpAddress = remoteIpAddress;
        context.Connection.RemotePort = remotePort;
        context.Request.Protocol = "HTTP/2";
        context.Response.StatusCode = StatusCodes.Status200OK;

        return context;
    }
}
