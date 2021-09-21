// <copyright file="TelemetryHandler.cs" company="OpenTelemetry Authors">
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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Contrib.Instrumentation.HttpTelemetryHandler.Implementation
{
    /// <summary>
    /// DiagnosticHandler notifies DiagnosticSource subscribers about outgoing Http requests.
    /// </summary>
    public sealed partial class TelemetryHandler : DelegatingHandler
    {
#if NET5_0_OR_GREATER
        /// <inheritdoc/>
        protected override HttpResponseMessage Send(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ValueTask<HttpResponseMessage> sendTask = this.SendAsyncCore(async: false, request, cancellationToken);
            Debug.Assert(sendTask.IsCompleted, "Task should completed");
            return sendTask.GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            this.SendAsyncCore(async: true, request, cancellationToken).AsTask();

        private async ValueTask<HttpResponseMessage> SendAsyncCore(
            bool async,
#else
        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(
#endif
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // HttpClientHandler is responsible to call static DiagnosticsHandler.IsEnabled() before forwarding request here.
            // It will check if propagation is on (because parent Activity exists or there is a listener) or off (forcibly disabled)
            // This code won't be called unless consumer unsubscribes from DiagnosticListener right after the check.
            // So some requests happening right after subscription starts might not be instrumented. Similarly,
            // when consumer unsubscribes, extra requests might be instrumented

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (HashHttpClientHandler(this.InnerHandler))
            {
                return
#if NET5_0_OR_GREATER
                    !async
                        ? base.Send(request, cancellationToken)
                        :
#endif
                        await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            Activity activity = null;
            DiagnosticListener diagnosticListener = Settings.DiagnosticListener;

            // if there is no listener, but propagation is enabled (with previous IsEnabled() check)
            // do not write any events just start/stop Activity and propagate Ids
            if (!IsEnabled() &&
                Propagators.DefaultTextMapPropagator.Extract(default, request, HttpRequestMessageContextPropagation.HeaderValuesGetter) == default)
            {
                activity = new Activity(DiagnosticsHandlerLoggingStrings.ActivityName);
                activity.Start();
                InjectHeaders(activity, request);

                try
                {
                    return
#if NET5_0_OR_GREATER
                        !async
                            ? base.Send(request, cancellationToken)
                            :
#endif
                            await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    activity.Stop();
                }
            }

            Guid loggingRequestId = Guid.Empty;

            // There is a listener. Check if listener wants to be notified about HttpClient Activities
            if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ActivityName, request))
            {
                activity = new Activity(DiagnosticsHandlerLoggingStrings.ActivityName);

                // Only send start event to users who subscribed for it, but start activity anyway
                if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ActivityStartName))
                {
                    diagnosticListener.StartActivity(activity, new ActivityStartData(request));
                }
                else
                {
                    activity.Start();
                }
            }

            // try to write System.Net.Http.Request event (deprecated)
            if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.RequestWriteNameDeprecated))
            {
                long timestamp = Stopwatch.GetTimestamp();
                loggingRequestId = Guid.NewGuid();
                diagnosticListener.Write(
                    DiagnosticsHandlerLoggingStrings.RequestWriteNameDeprecated,
                    new RequestData(request, loggingRequestId, timestamp));
            }

            // If we are on at all, we propagate current activity information
            Activity currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                InjectHeaders(currentActivity, request);
            }

            HttpResponseMessage response = null;
            TaskStatus taskStatus = TaskStatus.RanToCompletion;
            try
            {
                return response =
#if NET5_0_OR_GREATER
                    !async
                        ? base.Send(request, cancellationToken)
                        :
#endif
                        await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                taskStatus = TaskStatus.Canceled;

                // we'll report task status in HttpRequestOut.Stop
                throw;
            }
            catch (Exception ex)
            {
                taskStatus = TaskStatus.Faulted;

                if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ExceptionEventName))
                {
                    // If request was initially instrumented, Activity.Current has all necessary context for logging
                    // Request is passed to provide some context if instrumentation was disabled and to avoid
                    // extensive Activity.Tags usage to tunnel request properties
                    diagnosticListener.Write(DiagnosticsHandlerLoggingStrings.ExceptionEventName, new ExceptionData(ex, request));
                }

                throw;
            }
            finally
            {
                // always stop activity if it was started
                if (activity != null)
                {
                    // If request is failed or cancelled, there is no response, therefore no information about request;
                    // pass the request in the payload, so consumers can have it in Stop for failed/canceled requests
                    // and not retain all requests in Start
                    diagnosticListener.StopActivity(activity, new ActivityStopData(
                        response,
                        request,
                        taskStatus));
                }

                // Try to write System.Net.Http.Response event (deprecated)
                if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ResponseWriteNameDeprecated))
                {
                    long timestamp = Stopwatch.GetTimestamp();
                    diagnosticListener.Write(
                        DiagnosticsHandlerLoggingStrings.ResponseWriteNameDeprecated,
                        new ResponseData(response, loggingRequestId, timestamp, taskStatus));
                }
            }
        }

#pragma warning disable SA1204 // Static elements should appear before instance elements
        private static bool IsEnabled()
        {
            // check if there is a parent Activity (and propagation is not suppressed)
            // or if someone listens to HttpHandlerDiagnosticListener
            return IsGloballyEnabled() && (Activity.Current != null || Settings.DiagnosticListener.IsEnabled());
        }

        private static bool IsGloballyEnabled()
        {
            return Settings.ActivityPropagationEnabled;
        }

        private static bool HashHttpClientHandler(HttpMessageHandler handler)
        {
            while (handler != null)
            {
                switch (handler)
                {
                    case DelegatingHandler dh:
                        handler = dh.InnerHandler;
                        break;
                    case HttpClientHandler _:
                        return true;
                    default:
                        return false;
                }
            }

            return false;
        }

        private static void InjectHeaders(Activity currentActivity, HttpRequestMessage request)
        {
            if (currentActivity.IdFormat == ActivityIdFormat.W3C)
            {
                if (!request.Headers.Contains(DiagnosticsHandlerLoggingStrings.TraceParentHeaderName))
                {
                    request.Headers.TryAddWithoutValidation(DiagnosticsHandlerLoggingStrings.TraceParentHeaderName, currentActivity.Id);
                    if (currentActivity.TraceStateString != null)
                    {
                        request.Headers.TryAddWithoutValidation(DiagnosticsHandlerLoggingStrings.TraceStateHeaderName, currentActivity.TraceStateString);
                    }
                }
            }
            else
            {
                if (!request.Headers.Contains(DiagnosticsHandlerLoggingStrings.RequestIdHeaderName))
                {
                    request.Headers.TryAddWithoutValidation(DiagnosticsHandlerLoggingStrings.RequestIdHeaderName, currentActivity.Id);
                }
            }

            // we expect baggage to be empty or contain a few items
            using (IEnumerator<KeyValuePair<string, string>> e = currentActivity.Baggage.GetEnumerator())
            {
                if (e.MoveNext())
                {
                    var baggage = new List<string>();
                    do
                    {
                        KeyValuePair<string, string> item = e.Current;
                        baggage.Add(new NameValueHeaderValue(WebUtility.UrlEncode(item.Key), WebUtility.UrlEncode(item.Value)).ToString());
                    }
                    while (e.MoveNext());
                    request.Headers.TryAddWithoutValidation(DiagnosticsHandlerLoggingStrings.CorrelationContextHeaderName, baggage);
                }
            }
        }
#pragma warning restore SA1204 // Static elements should appear before instance elements
    }
}
