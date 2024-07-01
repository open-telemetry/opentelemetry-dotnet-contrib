// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests;

public class SampleLambdaContext : ILambdaContext
{
    public string AwsRequestId { get; } = "testrequestid";

    public IClientContext? ClientContext { get; }

    public string FunctionName { get; } = "testfunction";

    public string FunctionVersion { get; } = "latest";

    public ICognitoIdentity? Identity { get; }

    public string InvokedFunctionArn { get; } = "arn:aws:lambda:us-east-1:111111111111:function:testfunction";

    public ILambdaLogger? Logger { get; }

    public string? LogGroupName { get; }

    public string? LogStreamName { get; }

    public int MemoryLimitInMB { get; }

    public TimeSpan RemainingTime { get; }
}
