// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Tests;

public class GrpcTagHelperTests
{
    [Fact]
    public void GrpcTagHelper_GetGrpcMethodFromActivity()
    {
        var grpcMethod = "/some.service/somemethod";
        using var activity = new Activity("operationName");
        activity.SetTag(GrpcTagHelper.GrpcMethodTagName, grpcMethod);

        var result = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

        Assert.Equal(grpcMethod, result);
    }

    [Theory]
    [InlineData("Package.Service/Method", true, "Package.Service", "Method")]
    [InlineData("/Package.Service/Method", true, "Package.Service", "Method")]
    [InlineData("/ServiceWithNoPackage/Method", true, "ServiceWithNoPackage", "Method")]
    [InlineData("/Some.Package.Service/Method", true, "Some.Package.Service", "Method")]
    [InlineData("Invalid", false, "", "")]
    public void GrpcTagHelper_TryParseRpcServiceAndRpcMethod(string grpcMethod, bool isSuccess, string expectedRpcService, string expectedRpcMethod)
    {
        var success = GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod);

        Assert.Equal(isSuccess, success);
        Assert.Equal(expectedRpcService, rpcService);
        Assert.Equal(expectedRpcMethod, rpcMethod);
    }

    [Fact]
    public void GrpcTagHelper_GetGrpcStatusCodeFromActivity()
    {
        using var activity = new Activity("operationName");
        activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, "0");

        var validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out var status);
        Assert.True(validConversion);

        var statusCode = GrpcTagHelper.ResolveSpanStatusForGrpcStatusCodeOnClient(status);
        activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, status);

        Assert.Equal(ActivityStatusCode.Unset, statusCode);
        Assert.Equal(status, activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
    }

    [Theory]
    [InlineData(0, ActivityStatusCode.Unset)] // Ok
    [InlineData(1, ActivityStatusCode.Error)] // Cancelled
    [InlineData(2, ActivityStatusCode.Error)] // Unknown
    [InlineData(3, ActivityStatusCode.Error)] // InvalidArgument
    [InlineData(4, ActivityStatusCode.Error)] // DeadlineExceeded
    [InlineData(5, ActivityStatusCode.Error)] // NotFound
    [InlineData(6, ActivityStatusCode.Error)] // AlreadyExists
    [InlineData(7, ActivityStatusCode.Error)] // PermissionDenied
    [InlineData(8, ActivityStatusCode.Error)] // ResourceExhausted
    [InlineData(9, ActivityStatusCode.Error)] // FailedPrecondition
    [InlineData(10, ActivityStatusCode.Error)] // Aborted
    [InlineData(11, ActivityStatusCode.Error)] // OutOfRange
    [InlineData(12, ActivityStatusCode.Error)] // Unimplemented
    [InlineData(13, ActivityStatusCode.Error)] // Internal
    [InlineData(14, ActivityStatusCode.Error)] // Unavailable
    [InlineData(15, ActivityStatusCode.Error)] // DataLoss
    [InlineData(16, ActivityStatusCode.Error)] // Unauthenticated
    [InlineData(99, ActivityStatusCode.Error)] // Unknown status code
    public void GrpcTagHelper_ResolveSpanStatusForGrpcStatusCodeOnClient(int grpcStatusCode, ActivityStatusCode expectedActivityStatusCode)
    {
        var result = GrpcTagHelper.ResolveSpanStatusForGrpcStatusCodeOnClient(grpcStatusCode);
        Assert.Equal(expectedActivityStatusCode, result);
    }

    [Theory]
    [InlineData(0, ActivityStatusCode.Unset)] // Ok
    [InlineData(1, ActivityStatusCode.Unset)] // Cancelled
    [InlineData(2, ActivityStatusCode.Error)] // Unknown
    [InlineData(3, ActivityStatusCode.Unset)] // InvalidArgument
    [InlineData(4, ActivityStatusCode.Error)] // DeadlineExceeded
    [InlineData(5, ActivityStatusCode.Unset)] // NotFound
    [InlineData(6, ActivityStatusCode.Unset)] // AlreadyExists
    [InlineData(7, ActivityStatusCode.Unset)] // PermissionDenied
    [InlineData(8, ActivityStatusCode.Unset)] // ResourceExhausted
    [InlineData(9, ActivityStatusCode.Unset)] // FailedPrecondition
    [InlineData(10, ActivityStatusCode.Unset)] // Aborted
    [InlineData(11, ActivityStatusCode.Unset)] // OutOfRange
    [InlineData(12, ActivityStatusCode.Error)] // Unimplemented
    [InlineData(13, ActivityStatusCode.Error)] // Internal
    [InlineData(14, ActivityStatusCode.Error)] // Unavailable
    [InlineData(15, ActivityStatusCode.Error)] // DataLoss
    [InlineData(16, ActivityStatusCode.Unset)] // Unauthenticated
    [InlineData(99, ActivityStatusCode.Error)] // Unknown status code
    public void GrpcTagHelper_ResolveSpanStatusForGrpcStatusCodeOnServer(int grpcStatusCode, ActivityStatusCode expectedActivityStatusCode)
    {
        var result = GrpcTagHelper.ResolveSpanStatusForGrpcStatusCodeOnServer(grpcStatusCode);
        Assert.Equal(expectedActivityStatusCode, result);
    }

    [Fact]
    public void GrpcTagHelper_GetGrpcStatusCodeFromEmptyActivity()
    {
        using var activity = new Activity("operationName");

        var validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out var status);
        Assert.False(validConversion);
        Assert.Equal(-1, status);
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeRpcGrpcStatusCode));
    }
}
