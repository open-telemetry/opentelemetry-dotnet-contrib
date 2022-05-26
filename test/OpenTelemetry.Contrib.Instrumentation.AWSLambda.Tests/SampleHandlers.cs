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

namespace OpenTelemetry.Contrib.Instrumentation.AWSLambda.Tests
{
    public class SampleHandlers
    {
        public void SampleHandlerSyncNoReturn(string str, ILambdaContext context)
        {
        }

        public void SampleHandlerSyncNoReturn(string str)
        {
        }

        public string SampleHandlerSyncReturn(string str, ILambdaContext context)
        {
            return str;
        }

        public string SampleHandlerSyncReturn(string str)
        {
            return str;
        }

        public async Task SampleHandlerAsyncNoReturn(string str, ILambdaContext context)
        {
            await Task.Delay(10);
        }

        public async Task SampleHandlerAsyncNoReturn(string str)
        {
            await Task.Delay(10);
        }

        public async Task<string> SampleHandlerAsyncReturn(string str, ILambdaContext context)
        {
            await Task.Delay(10);
            return str;
        }

        public async Task<string> SampleHandlerAsyncReturn(string str)
        {
            await Task.Delay(10);
            return str;
        }

        public void SampleHandlerSyncNoReturnException(string str, ILambdaContext context)
        {
            throw new Exception(str);
        }
    }
}
