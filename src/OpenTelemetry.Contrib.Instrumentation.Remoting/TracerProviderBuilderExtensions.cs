// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
    /// <summary>
    /// Extension methods to simplify registering .NET Remoting instrumentation.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Enables .NET Remoting instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddRemotingInstrumentation(this TracerProviderBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddSource(TelemetryDynamicSink.ActivitySourceName);

            // See https://docs.microsoft.com/en-us/dotnet/api/system.runtime.remoting.contexts.context.registerdynamicproperty?view=netframework-4.8
            // This will register our dynamic sink to listen to all calls leaving or entering
            // current AppDomain.
            RemotingContext.RegisterDynamicProperty(
                new TelemetryDynamicSinkProvider(),
                null,
                RemotingContext.DefaultContext);

            return builder;
        }
    }
}
