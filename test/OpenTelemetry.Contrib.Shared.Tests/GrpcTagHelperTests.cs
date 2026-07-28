// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

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
    [InlineData("/some.service/somemethod", "some.service/somemethod")]
    [InlineData("some.service/somemethod", "some.service/somemethod")]
    public void GrpcTagHelper_SetGrpcMethodAndDisplayNameFromActivity_RecognizedMethod(string grpcMethod, string expected)
    {
        using var activity = new Activity("operationName");
        activity.SetTag(GrpcTagHelper.GrpcMethodTagName, grpcMethod);

        GrpcTagHelper.SetGrpcMethodAndDisplayNameFromActivity(activity);

        Assert.Equal(expected, activity.DisplayName);
        Assert.Equal(expected, activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeRpcMethodOriginal));
        Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
    }

    [Theory]
    [InlineData("other")]
    [InlineData("/other")]
    public void GrpcTagHelper_SetGrpcMethodAndDisplayNameFromActivity_UnrecognizedMethod(string grpcMethod)
    {
        using var activity = new Activity("operationName");
        activity.SetTag(GrpcTagHelper.GrpcMethodTagName, grpcMethod);

        GrpcTagHelper.SetGrpcMethodAndDisplayNameFromActivity(activity);

        Assert.Equal(GrpcTagHelper.RpcSystemGrpc, activity.DisplayName);
        Assert.Equal(GrpcTagHelper.RpcMethodOther, activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
        Assert.Equal(GrpcTagHelper.GrpcMethodOther, activity.GetTagValue(SemanticConventions.AttributeRpcMethodOriginal));
        Assert.Null(activity.GetTagValue(GrpcTagHelper.GrpcMethodTagName));
    }

    [Fact]
    public void GrpcTagHelper_SetGrpcMethodAndDisplayNameFromActivity_NoMethod()
    {
        using var activity = new Activity("operationName");

        GrpcTagHelper.SetGrpcMethodAndDisplayNameFromActivity(activity);

        Assert.Equal("operationName", activity.DisplayName);
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeRpcMethod));
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeRpcMethodOriginal));
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
    [InlineData(-1, ActivityStatusCode.Error)] // Invalid negative status code
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
    [InlineData(-1, ActivityStatusCode.Error)] // Invalid negative status code
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
        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeRpcResponseStatusCode));
    }

    [Theory]
    [InlineData(int.MinValue, "-2147483648")]
    [InlineData(-1, "-1")]
    [InlineData(0, "OK")]
    [InlineData(1, "CANCELLED")]
    [InlineData(2, "UNKNOWN")]
    [InlineData(3, "INVALID_ARGUMENT")]
    [InlineData(4, "DEADLINE_EXCEEDED")]
    [InlineData(5, "NOT_FOUND")]
    [InlineData(6, "ALREADY_EXISTS")]
    [InlineData(7, "PERMISSION_DENIED")]
    [InlineData(8, "RESOURCE_EXHAUSTED")]
    [InlineData(9, "FAILED_PRECONDITION")]
    [InlineData(10, "ABORTED")]
    [InlineData(11, "OUT_OF_RANGE")]
    [InlineData(12, "UNIMPLEMENTED")]
    [InlineData(13, "INTERNAL")]
    [InlineData(14, "UNAVAILABLE")]
    [InlineData(15, "DATA_LOSS")]
    [InlineData(16, "UNAUTHENTICATED")]
    [InlineData(99, "99")]
    [InlineData(int.MaxValue, "2147483647")]
    public void GrpcTagHelper_ConvertStatusCodeToString(int statusCode, string expected)
    {
        var actual = GrpcTagHelper.GetGrpcStatusCodeName(statusCode);
        Assert.Equal(expected, actual);
    }
}
