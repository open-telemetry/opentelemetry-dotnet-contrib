﻿// <copyright file="DiagnosticsMiddleware.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Owin;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.Owin
{
    /// <summary>
    /// Instruments incoming request with <see cref="Activity"/> and notifies listeners with <see cref="ActivitySource"/>.
    /// </summary>
    internal sealed class DiagnosticsMiddleware : OwinMiddleware
    {
        private const string ContextKey = "__OpenTelemetry.Context__";
        private static readonly Func<IOwinRequest, string, IEnumerable<string>> OwinRequestHeaderValuesGetter
            = (request, name) => request.Headers.GetValues(name);

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsMiddleware"/> class.
        /// </summary>
        /// <param name="next">An optional pointer to the next component.</param>
        public DiagnosticsMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        /// <inheritdoc />
        public override async Task Invoke(IOwinContext owinContext)
        {
            try
            {
                BeginRequest(owinContext);
                await this.Next.Invoke(owinContext).ConfigureAwait(false);
                RequestEnd(owinContext, null);
            }
            catch (Exception ex)
            {
                RequestEnd(owinContext, ex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BeginRequest(IOwinContext owinContext)
        {
            try
            {
                if (OwinInstrumentationActivitySource.Options == null || OwinInstrumentationActivitySource.Options.Filter?.Invoke(owinContext) == false)
                {
                    OwinInstrumentationEventSource.Log.RequestIsFilteredOut();
                    return;
                }
            }
            catch (Exception ex)
            {
                OwinInstrumentationEventSource.Log.RequestFilterException(ex);
                return;
            }

            var textMapPropagator = OwinInstrumentationActivitySource.Options.Propagator;
            var ctx = textMapPropagator.Extract(default, owinContext.Request, OwinRequestHeaderValuesGetter);

            Activity activity = OwinInstrumentationActivitySource.ActivitySource.StartActivity(
                OwinInstrumentationActivitySource.IncomingRequestActivityName,
                ActivityKind.Server,
                ctx.ActivityContext);

            if (activity != null)
            {
                var request = owinContext.Request;

                /*
                 * Note: Display name is intentionally set to a low cardinality
                 * value because OWIN does not expose any kind of
                 * route/template. See:
                 * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md#name
                 */
                activity.DisplayName = request.Method switch
                {
                    "GET" => "HTTP GET",
                    "POST" => "HTTP POST",
                    "PUT" => "HTTP PUT",
                    "DELETE" => "HTTP DELETE",
                    _ => $"HTTP {request.Method}",
                };

                if (activity.IsAllDataRequested)
                {
                    if (request.Uri.Port == 80 || request.Uri.Port == 443)
                    {
                        activity.SetTag(SemanticConventions.AttributeHttpHost, request.Uri.Host);
                    }
                    else
                    {
                        activity.SetTag(SemanticConventions.AttributeHttpHost, request.Uri.Host + ":" + request.Uri.Port);
                    }

                    activity.SetTag(SemanticConventions.AttributeHttpMethod, request.Method);
                    activity.SetTag(SemanticConventions.AttributeHttpTarget, request.Uri.AbsolutePath);
                    activity.SetTag(SemanticConventions.AttributeHttpUrl, GetUriTagValueFromRequestUri(request.Uri));

                    if (request.Headers.TryGetValue("User-Agent", out string[] userAgent) && userAgent.Length > 0)
                    {
                        activity.SetTag(SemanticConventions.AttributeHttpUserAgent, userAgent[0]);
                    }

                    try
                    {
                        OwinInstrumentationActivitySource.Options.Enrich?.Invoke(
                            activity,
                            OwinEnrichEventNames.BeginRequest,
                            owinContext,
                            null);
                    }
                    catch (Exception ex)
                    {
                        OwinInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }

                if (!(textMapPropagator is TraceContextPropagator))
                {
                    Baggage.Current = ctx.Baggage;
                }

                owinContext.Environment[ContextKey] = activity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RequestEnd(IOwinContext owinContext, Exception exception)
        {
            if (owinContext.Environment.TryGetValue(ContextKey, out object context)
                && context is Activity activity)
            {
                if (Activity.Current != activity)
                {
                    Activity.Current = activity;
                }

                if (activity.IsAllDataRequested)
                {
                    var response = owinContext.Response;

                    if (exception != null)
                    {
                        activity.SetStatus(Status.Error);

                        if (OwinInstrumentationActivitySource.Options.RecordException)
                        {
                            activity.RecordException(exception);
                        }
                    }
                    else if (activity.GetStatus().StatusCode == StatusCode.Unset)
                    {
                        activity.SetStatus(SpanHelper.ResolveSpanStatusForHttpStatusCode(response.StatusCode));
                    }

                    activity.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode);

                    try
                    {
                        OwinInstrumentationActivitySource.Options.Enrich?.Invoke(
                            activity,
                            OwinEnrichEventNames.EndRequest,
                            owinContext,
                            exception);
                    }
                    catch (Exception ex)
                    {
                        OwinInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }

                activity.Stop();

                if (!(OwinInstrumentationActivitySource.Options.Propagator is TraceContextPropagator))
                {
                    Baggage.Current = default;
                }
            }
        }

        /// <summary>
        /// Gets the OpenTelemetry standard uri tag value for a span based on its request <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri"><see cref="Uri"/>.</param>
        /// <returns>Span uri value.</returns>
        private static string GetUriTagValueFromRequestUri(Uri uri)
        {
            if (string.IsNullOrEmpty(uri.UserInfo))
            {
                return uri.ToString();
            }

            return string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.PathAndQuery, uri.Fragment);
        }
    }
}
