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
using OpenTelemetry.Contrib.Extensions.AWSXRay;
using Opentelemetry.Contrib.Extensions.AWSXRay.Instrumentation;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension method to generate AWS X-Ray compatible trace id and replace the trace id of root activity.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Replace the trace id of root activity.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/>.</returns>
        public static TracerProviderBuilder AddXRayActivityTraceIdGenerator(this TracerProviderBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            AWSXRayIdGenerator.ReplaceTraceId();
            return builder;
        }

        /// <summary>
        /// Enables AWS Instrumentation.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddAWSInstrumentation(
            this TracerProviderBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            new AWSClientsInstrumentation();
            builder.AddSource("Amazon.AWS.AWSClientInstrumentation");
            return builder;
        }
    }
}
