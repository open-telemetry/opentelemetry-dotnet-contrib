// <copyright file="SampleHandlers.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Amazon.Lambda.Core;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests;

internal class SampleHandlers
{
    // Action<TInput, ILambdaContext>
    public void SampleHandlerSyncInputAndNoReturn(string str, ILambdaContext? context)
    {
    }

    // Func<TInput, ILambdaContext, TResult>
    public string SampleHandlerSyncInputAndReturn(string str, ILambdaContext? context)
    {
        return str;
    }

    // Func<TInput, ILambdaContext, Task>
    public async Task SampleHandlerAsyncInputAndNoReturn(string str, ILambdaContext? context)
    {
        await Task.Delay(10);
    }

    // Func<TInput, ILambdaContext, Task<TResult>>
    public async Task<string> SampleHandlerAsyncInputAndReturn(string str, ILambdaContext? context)
    {
        await Task.Delay(10);
        return str;
    }

    public void SampleHandlerSyncNoReturnException(string str, ILambdaContext? context)
    {
        throw new Exception(str);
    }
}
