// <copyright file="IntakeApiVersion.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Exporter.Elastic
{
    /// <summary>
    /// Elastic APM Server events intake API versions.
    /// </summary>
    public sealed class IntakeApiVersion
    {
        /// <summary>
        /// Intake API v2.
        /// </summary>
        public static readonly IntakeApiVersion V2 = new IntakeApiVersion("/intake/v2/events");

        private readonly string endpoint;

        private IntakeApiVersion(string endpoint)
        {
            this.endpoint = endpoint;
        }

        /// <summary>
        /// Implicit operator to string for endpoint field.
        /// </summary>
        /// <param name="value">The intake API version.</param>
        public static implicit operator string(IntakeApiVersion value) => value.endpoint;
    }
}
