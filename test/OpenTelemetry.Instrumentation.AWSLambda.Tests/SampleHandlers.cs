// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests;

internal class SampleHandlers
{
    // Action<TInput, ILambdaContext>
    public void SampleHandlerSyncInputAndNoReturn(string str, ILambdaContext context)
    {
    }

    // Func<TInput, ILambdaContext, TResult>
    public string SampleHandlerSyncInputAndReturn(string str, ILambdaContext context)
    {
        return str;
    }

    // Func<TInput, ILambdaContext, Task>
    public async Task SampleHandlerAsyncInputAndNoReturn(string str, ILambdaContext context)
    {
        await Task.Delay(10);
    }

    // Func<TInput, ILambdaContext, Task<TResult>>
    public async Task<string> SampleHandlerAsyncInputAndReturn(string str, ILambdaContext context)
    {
        await Task.Delay(10);
        return str;
    }

    public void SampleHandlerSyncNoReturnException(string str, ILambdaContext context)
    {
        throw new Exception(str);
    }
}
