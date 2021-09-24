﻿// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
        /// <param name="configureRemotingInstrumentationOptions">Instrumentation options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddRemotingInstrumentation(
            this TracerProviderBuilder builder,
            Action<RemotingInstrumentationOptions> configureRemotingInstrumentationOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddSource(TelemetryDynamicSink.ActivitySourceName);

            var remotingOptions = new RemotingInstrumentationOptions();
            configureRemotingInstrumentationOptions?.Invoke(remotingOptions);

            builder.AddInstrumentation(() => new RemotingInstrumentation(remotingOptions));

            return builder;
        }
    }
}
