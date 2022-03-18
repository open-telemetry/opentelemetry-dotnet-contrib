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

namespace OpenTelemetry.Trace
{
    using System;
    using OpenTelemetry.Instrumentation.GrpcCore;

    /// <summary>
    /// OpenTelemetry builder extensions to simplify registration of Grpc.Core based interceptors.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Configures OpenTelemetry to listen for the Activities created by the client and server interceptors.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddGrpcCoreInstrumentation(
            this TracerProviderBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddSource(GrpcCoreInstrumentation.ActivitySourceName);
        }
    }
}
