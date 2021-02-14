// <copyright file="WcfInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.ServiceModel.Channels;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Contrib.Instrumentation.Wcf
{
    /// <summary>
    /// Options for WCF instrumentation.
    /// </summary>
    public class WcfInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets <see cref="TextMapPropagator"/> for context propagation. Default value: <see cref="CompositeTextMapPropagator"/> with <see cref="TraceContextPropagator"/> &amp; <see cref="BaggagePropagator"/>.
        /// </summary>
        public TextMapPropagator Propagator { get; set; } = new CompositeTextMapPropagator(new TextMapPropagator[]
        {
            new TraceContextPropagator(),
            new BaggagePropagator(),
        });

        /// <summary>
        /// Gets or sets a Filter function to filter instrumentation for requests on a per request basis.
        /// The Filter gets the Message, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<Message, bool> IncomingRequestFilter { get; set; }

        /// <summary>
        /// Gets or sets a Filter function to filter instrumentation for requests on a per request basis.
        /// The Filter gets the Message, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<Message, bool> OutgoingRequestFilter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether down stream instrumentation (HttpClient) is suppressed (disabled).
        /// </summary>
        public bool SuppressDownstreamInstrumentation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the SOAP version should be added as the <see cref="WcfInstrumentationConstants.SoapVersionTag"/> tag. Default value: False.
        /// </summary>
        public bool SetSoapVersion { get; set; }
    }
}
