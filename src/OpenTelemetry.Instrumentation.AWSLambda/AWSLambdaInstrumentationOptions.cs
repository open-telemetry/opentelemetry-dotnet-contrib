// <copyright file="AWSLambdaInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.AWSLambda;

/// <summary>
/// AWS lambda instrumentation options.
/// </summary>
public class AWSLambdaInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether AWS X-Ray context extraction should be disabled.
    /// </summary>
    public bool DisableAwsXRayContextExtraction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the parent Activity should be set when a potentially batched event is received where multiple parents are potentially available (e.g. SQS).
    /// If set to true, the parent is set using the last received record (e.g. last message). Otherwise the parent is not set. In both cases, links will be created for such events.
    /// </summary>
    /// <remarks>
    /// Currently, the only event type to which this applies is SQS.
    /// </remarks>
    public bool SetParentFromBatch { get; set; }
}
