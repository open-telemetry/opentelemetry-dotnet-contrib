// <copyright file="OwinInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Microsoft.Owin;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.Owin
{
    /// <summary>
    /// Options for requests instrumentation.
    /// </summary>
    public class OwinInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a Filter function that determines whether or not to collect telemetry about requests on a per request basis.
        /// The Filter gets the <see cref="IOwinContext"/>, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<IOwinContext, bool> Filter { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich the <see cref="Activity"/> created by the instrumentation.
        /// </summary>
        public Action<Activity, OwinEnrichEventType, IOwinContext, Exception> Enrich { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the exception will be recorded as <see cref="ActivityEvent"/> or not.
        /// </summary>
        /// <remarks>
        /// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md.
        /// </remarks>
        public bool RecordException { get; set; }
    }
}
