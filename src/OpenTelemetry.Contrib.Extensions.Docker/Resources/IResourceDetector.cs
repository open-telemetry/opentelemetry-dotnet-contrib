// <copyright file="IResourceDetector.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Contrib.Extensions.Docker.Resources
{
    /// <summary>
    /// Resource detector interface.
    /// Mocking https://github.com/open-telemetry/opentelemetry-dotnet/blob/6b7f2dd77cf9d37260a853fcc95f7b77e296065d/src/OpenTelemetry/Resources/IResourceDetector.cs.
    /// </summary>
    public interface IResourceDetector
    {
        /// <summary>
        /// Called to get key-value pairs of attribute from detector.
        /// </summary>
        /// <returns>List of key-value pairs of resource attributes.</returns>
        IEnumerable<KeyValuePair<string, object>> Detect();
    }
}
