// <copyright file="ClientTracingInterceptorOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.GrpcCore
{
    using OpenTelemetry.Context.Propagation;

    /// <summary>
    /// A delegate for setting a Activity tag.
    /// </summary>
    /// <param name="tagName">Name of the tag.</param>
    /// <param name="tagValue">The tag value.</param>
    public delegate void SpanAttributeSetter(string tagName, object tagValue);

    /// <summary>
    /// Options for the ClientTracingInterceptor.
    /// </summary>
    public class ClientTracingInterceptorOptions
    {
        /// <summary>
        /// The default propagator.
        /// </summary>
        private static readonly TextMapPropagator DefaultPropagator = new TraceContextPropagator();

        /// <summary>
        /// Gets or sets a value indicating whether gets a flag indicating whether or not to record individual message events.
        /// </summary>
        public bool RecordMessageEvents { get; set; } = false;

        /// <summary>
        /// Gets or sets the propagator.
        /// </summary>
        public TextMapPropagator Propagator { get; set; } = DefaultPropagator;
    }
}
