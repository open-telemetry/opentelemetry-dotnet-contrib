// <copyright file="RemotingInstrumentation.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Contrib.Instrumentation.Remoting.Implementation;
using RemotingContext = System.Runtime.Remoting.Contexts.Context;

namespace OpenTelemetry.Trace
{
    internal class RemotingInstrumentation : IDisposable
    {
        public RemotingInstrumentation(RemotingInstrumentationOptions options)
        {
            // See https://docs.microsoft.com/en-us/dotnet/api/system.runtime.remoting.contexts.context.registerdynamicproperty?view=netframework-4.8
            // This will register our dynamic sink to listen to all calls leaving or entering
            // current AppDomain.
            RemotingContext.RegisterDynamicProperty(
                new TelemetryDynamicSinkProvider(options),
                null,
                RemotingContext.DefaultContext);
        }

        public void Dispose()
        {
            // Un-register ourselves on dispose.
            RemotingContext.UnregisterDynamicProperty(
                TelemetryDynamicSinkProvider.DynamicPropertyName,
                null,
                RemotingContext.DefaultContext);
        }
    }
}
