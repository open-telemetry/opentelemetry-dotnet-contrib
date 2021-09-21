// <copyright file="TelemetryHandler.Nest.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;

namespace OpenTelemetry.Contrib.Instrumentation.HttpTelemetryHandler.Implementation
{
    /// <summary>
    /// DiagnosticHandler notifies DiagnosticSource subscribers about outgoing Http requests.
    /// </summary>
    public partial class TelemetryHandler
    {
        private static class Settings
        {
            public static readonly bool ActivityPropagationEnabled = GetEnableActivityPropagationValue();
            public static readonly DiagnosticListener DiagnosticListener =
                new DiagnosticListener(DiagnosticsHandlerLoggingStrings.DiagnosticListenerName);

            private const string EnableActivityPropagationEnvironmentVariableSettingName = "DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION";
            private const string EnableActivityPropagationAppCtxSettingName = "System.Net.Http.EnableActivityPropagation";

            private static bool GetEnableActivityPropagationValue()
            {
                // First check for the AppContext switch, giving it priority over the environment variable.
                if (AppContext.TryGetSwitch(EnableActivityPropagationAppCtxSettingName, out bool enableActivityPropagation))
                {
                    return enableActivityPropagation;
                }

                // AppContext switch wasn't used. Check the environment variable to determine which handler should be used.
                string envVar = Environment.GetEnvironmentVariable(EnableActivityPropagationEnvironmentVariableSettingName);
                if (envVar != null && (envVar.Equals("false", StringComparison.OrdinalIgnoreCase) || envVar.Equals("0")))
                {
                    // Suppress Activity propagation.
                    return false;
                }

                // Defaults to enabling Activity propagation.
                return true;
            }
        }

        private sealed class ActivityStartData
        {
            internal ActivityStartData(HttpRequestMessage request)
            {
                this.Request = request;
            }

            public HttpRequestMessage Request { get; }

            public override string ToString() => $"{{ {nameof(this.Request)} = {this.Request} }}";
        }

        private sealed class ActivityStopData
        {
            internal ActivityStopData(HttpResponseMessage response, HttpRequestMessage request, TaskStatus requestTaskStatus)
            {
                this.Response = response;
                this.Request = request;
                this.RequestTaskStatus = requestTaskStatus;
            }

            public HttpResponseMessage Response { get; }

            public HttpRequestMessage Request { get; }

            public TaskStatus RequestTaskStatus { get; }

            public override string ToString() => $"{{ {nameof(this.Response)} = {this.Response}, {nameof(this.Request)} = {this.Request}, {nameof(this.RequestTaskStatus)} = {this.RequestTaskStatus} }}";
        }

        private sealed class ExceptionData
        {
            internal ExceptionData(Exception exception, HttpRequestMessage request)
            {
                this.Exception = exception;
                this.Request = request;
            }

            public Exception Exception { get; }

            public HttpRequestMessage Request { get; }

            public override string ToString() => $"{{ {nameof(this.Exception)} = {this.Exception}, {nameof(this.Request)} = {this.Request} }}";
        }

        private sealed class RequestData
        {
            internal RequestData(HttpRequestMessage request, Guid loggingRequestId, long timestamp)
            {
                this.Request = request;
                this.LoggingRequestId = loggingRequestId;
                this.Timestamp = timestamp;
            }

            public HttpRequestMessage Request { get; }

            public Guid LoggingRequestId { get; }

            public long Timestamp { get; }

            public override string ToString() => $"{{ {nameof(this.Request)} = {this.Request}, {nameof(this.LoggingRequestId)} = {this.LoggingRequestId}, {nameof(this.Timestamp)} = {this.Timestamp} }}";
        }

        private sealed class ResponseData
        {
            internal ResponseData(HttpResponseMessage response, Guid loggingRequestId, long timestamp, TaskStatus requestTaskStatus)
            {
                this.Response = response;
                this.LoggingRequestId = loggingRequestId;
                this.Timestamp = timestamp;
                this.RequestTaskStatus = requestTaskStatus;
            }

            public HttpResponseMessage Response { get; }

            public Guid LoggingRequestId { get; }

            public long Timestamp { get; }

            public TaskStatus RequestTaskStatus { get; }

            public override string ToString() => $"{{ {nameof(this.Response)} = {this.Response}, {nameof(this.LoggingRequestId)} = {this.LoggingRequestId}, {nameof(this.Timestamp)} = {this.Timestamp}, {nameof(this.RequestTaskStatus)} = {this.RequestTaskStatus} }}";
        }
    }
}
