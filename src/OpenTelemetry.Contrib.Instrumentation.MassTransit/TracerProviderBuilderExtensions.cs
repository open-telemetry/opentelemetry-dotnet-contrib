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
using OpenTelemetry.Instrumentation.MassTransit;
using OpenTelemetry.Instrumentation.MassTransit.Implementation;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of dependency instrumentation.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Enables the outgoing requests automatic data collection for MassTransit.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilderExtensions"/> being configured.</param>
        /// <param name="configureMassTransitInstrumentationOptions">MassTransit configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilderExtensions"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddMassTransitInstrumentation(
            this TracerProviderBuilder builder,
            Action<MassTransitInstrumentationOptions> configureMassTransitInstrumentationOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var options = new MassTransitInstrumentationOptions();
            configureMassTransitInstrumentationOptions?.Invoke(options);

            builder.AddInstrumentation(() => new MassTransitInstrumentation(options));
            builder.AddSource(MassTransitDiagnosticListener.ActivitySourceName);

            builder.AddLegacySource(OperationName.Consumer.Consume);
            builder.AddLegacySource(OperationName.Consumer.Handle);
            builder.AddLegacySource(OperationName.Transport.Send);
            builder.AddLegacySource(OperationName.Transport.Receive);

            return builder;
        }
    }
}
