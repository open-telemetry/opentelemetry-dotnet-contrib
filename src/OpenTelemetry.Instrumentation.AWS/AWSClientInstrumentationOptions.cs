// <copyright file="AWSClientInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.AWS;

/// <summary>
/// Options for AWS client instrumentation.
/// </summary>
public class AWSClientInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether downstream Http instrumentation is suppressed.
    /// </summary>
    public bool SuppressDownstreamInstrumentation { get; set; } = true;
}
