// <copyright file="TelemetryPropagationWriter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

/// <summary>
/// Pre-defined PropagationWriter callbacks.
/// </summary>
internal static class TelemetryPropagationWriter
{
    /// <summary>
    /// Writes the values to SOAP message headers. If the message is not a SOAP message it does nothing.
    /// </summary>
    /// <param name="request">The outgoing request <see cref="Message"/> (carrier) to write trace context to.</param>
    /// <param name="name">The header name being written.</param>
    /// <param name="value">The header value to write.</param>
    public static void SoapMessageHeaders(Message request, string name, string value)
    {
        Guard.ThrowIfNull(request);
        Guard.ThrowIfNull(name);
        Guard.ThrowIfNull(value);

        if (request.Version == MessageVersion.None)
        {
            return;
        }

        request.Headers.Add(TelemetryMessageHeader.CreateHeader(name, value));
    }

    /// <summary>
    /// Writes the values to outgoing HTTP request headers. If the message is not ultimately transmitted via HTTP these will be ignored.
    /// </summary>
    /// <param name="request">The outgoing request <see cref="Message"/> (carrier) to write trace context to.</param>
    /// <param name="name">The header name being written.</param>
    /// <param name="value">The header value to write.</param>
    public static void HttpRequestHeaders(Message request, string name, string value)
    {
        Guard.ThrowIfNull(request);
        Guard.ThrowIfNull(name);
        Guard.ThrowIfNull(value);

        if (!HttpRequestMessagePropertyWrapper.IsHttpFunctionalityEnabled)
        {
            return;
        }

        object prop;
        if (!request.Properties.TryGetValue(HttpRequestMessagePropertyWrapper.Name, out prop))
        {
            prop = HttpRequestMessagePropertyWrapper.CreateNew();
            request.Properties.Add(HttpRequestMessagePropertyWrapper.Name, prop);
        }

        HttpRequestMessagePropertyWrapper.GetHeaders(prop)[name] = value;
    }

    /// <summary>
    /// Writes values to both SOAP message headers and outgoing HTTP request headers when suppressing downstream instrumentation.
    /// When not suppressing downstream instrumentation it is assumed the transport will be propagating the context itself, so
    /// only SOAP message headers are written.
    /// </summary>
    /// <param name="request">The outgoing request <see cref="Message"/> (carrier) to write trace context to.</param>
    /// <param name="name">The header name being written.</param>
    /// <param name="value">The header value to write.</param>
    public static void Default(Message request, string name, string value)
    {
        SoapMessageHeaders(request, name, value);
        if (WcfInstrumentationActivitySource.Options.SuppressDownstreamInstrumentation)
        {
            HttpRequestHeaders(request, name, value);
        }
    }

    /// <summary>
    /// Compose multiple PropagationWriter callbacks into a single callback. All callbacks
    /// are executed to allow for propagating the context in multiple ways.
    /// </summary>
    /// <param name="callbacks">The callbacks to compose into a single callback.</param>
    /// <returns>The composed callback.</returns>
    public static Action<Message, string, string> Compose(params Action<Message, string, string>[] callbacks)
    {
        Guard.ThrowIfNull(callbacks);
        Array.ForEach(callbacks, cb => Guard.ThrowIfNull(cb));

        return (Message request, string name, string value) =>
        {
            foreach (var writer in callbacks)
            {
                writer(request, name, value);
            }
        };
    }
}
