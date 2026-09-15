// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;
using Amazon.Lambda.SQSEvents;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests;

internal class SampleHandlers
{
    // Action<TInput, ILambdaContext>
#pragma warning disable SA1313
    public void SampleHandlerSyncInputAndNoReturn(string _1, ILambdaContext _2)
    {
    }

    // Func<TInput, ILambdaContext, TResult>
    public string SampleHandlerSyncInputAndReturn(string str, ILambdaContext _2)
    {
        return str;
    }

    // Action<TInput, ILambdaContext> for an SQS event source.
    public void SampleHandlerSyncSqsEvent(SQSEvent _1, ILambdaContext _2)
    {
    }

    // Action<TInput, ILambdaContext> for a single SQS message.
    public void SampleHandlerSyncSqsMessage(SQSEvent.SQSMessage _1, ILambdaContext _2)
    {
    }

    // Action<TInput, ILambdaContext> for an SNS event source.
    public void SampleHandlerSyncSnsEvent(SNSEvent _1, ILambdaContext _2)
    {
    }

    // Action<TInput, ILambdaContext> for a single SNS record.
    public void SampleHandlerSyncSnsRecord(SNSEvent.SNSRecord _1, ILambdaContext _2)
    {
    }

    // Func<TInput, ILambdaContext, Task>
    public async Task SampleHandlerAsyncInputAndNoReturn(string _1, ILambdaContext _2)
    {
        await Task.Delay(10);
    }

    // Func<TInput, ILambdaContext, Task<TResult>>
    public async Task<string> SampleHandlerAsyncInputAndReturn(string str, ILambdaContext _2)
    {
        await Task.Delay(10);
        return str;
    }

    // Action<TInput, ILambdaContext>
    public void SampleHandlerSyncNoReturnException(string str, ILambdaContext _2)
#pragma warning restore SA1313
    {
        throw new Exception(str);
    }
}
