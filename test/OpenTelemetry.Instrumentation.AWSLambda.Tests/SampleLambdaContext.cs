// <copyright file="SampleLambdaContext.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
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
