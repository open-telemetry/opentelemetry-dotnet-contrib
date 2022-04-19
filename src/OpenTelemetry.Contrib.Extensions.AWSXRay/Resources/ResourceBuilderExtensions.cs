// <copyright file="ResourceBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources
{
    /// <summary>
    /// Extension class for ResourceBuilder.
    /// </summary>
    public static class ResourceBuilderExtensions
    {
        /// <summary>
        /// Add resource detector to ResourceBuilder.
        /// </summary>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> being configured.</param>
        /// <param name="resourceDetector"><see cref="IResourceDetector"/> being added.</param>
        /// <returns>The instance of <see cref="ResourceBuilder"/> to chain the calls.</returns>
        public static ResourceBuilder AddDetector(this ResourceBuilder resourceBuilder, IResourceDetector resourceDetector)
        {
            Guard.ThrowIfNull(resourceDetector);

            var resourceAttributes = resourceDetector.Detect();

            if (resourceAttributes != null)
            {
                resourceBuilder.AddAttributes(resourceAttributes);
            }

            return resourceBuilder;
        }
    }
}
