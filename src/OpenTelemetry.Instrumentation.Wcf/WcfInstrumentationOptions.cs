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
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel.Channels;

namespace OpenTelemetry.Instrumentation.Wcf;

/// <summary>
/// Options for WCF instrumentation.
/// </summary>
public class WcfInstrumentationOptions
{
    /// <summary>
    /// Gets or sets an action to enrich an Activity.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Activity"/>: the activity being enriched.</para>
    /// <para>string: the name of the event. Will be one of the constants in <see cref="WcfEnrichEventNames"/>.
    /// </para>
    /// <para>object: the raw <see cref="Message"/> from which additional information can be extracted to enrich the activity.
    /// </para>
    /// </remarks>
    public Action<Activity, string, object> Enrich { get; set; }

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
    /// Gets or sets a value indicating whether down stream instrumentation (HttpClient) is suppressed (disabled). Default value: True.
    /// </summary>
    public bool SuppressDownstreamInstrumentation { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether or not the SOAP message version should be added as the <see cref="WcfInstrumentationConstants.SoapMessageVersionTag"/> tag. Default value: False.
    /// </summary>
    public bool SetSoapMessageVersion { get; set; }

    /// <summary>
    /// Gets or sets the reader function which controls exactly how the propagated trace context is extracted from the incoming request.
    /// Default value: Composed <see cref="TelemetryPropagationReader.HttpRequestHeaders"/> and <see cref="TelemetryPropagationReader.SoapMessageHeaders"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Message"/>: The incoming request (carrier) to read trace context from.</para>
    /// <para>string: The header name being requested.</para>
    /// <para>IEnumerable&lt;string&gt; (return): All the values for the requested header, or null if the requested header was not present on the request.</para>
    /// </remarks>
    public Func<Message, string, IEnumerable<string>> PropagationReader { get; set; } = TelemetryPropagationReader.Default;

    /// <summary>
    /// Gets or sets the writer function which controls exactly how the propagated trace context is inscribed on the outgoing request.
    /// Default value: When <see cref="SuppressDownstreamInstrumentation"/> is true it is composed <see cref="TelemetryPropagationWriter.SoapMessageHeaders"/> and
    /// <see cref="TelemetryPropagationWriter.HttpRequestHeaders"/>. When <see cref="SuppressDownstreamInstrumentation"/> is false it is just
    /// <see cref="TelemetryPropagationWriter.SoapMessageHeaders"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Message"/>: The outgoing request (carrier) to write trace context to.</para>
    /// <para>string: The name of the header being written.</para>
    /// <para>string: The header value to be written.</para>
    /// </remarks>
    public Action<Message, string, string> PropagationWriter { get; set; } = TelemetryPropagationWriter.Default;
}
