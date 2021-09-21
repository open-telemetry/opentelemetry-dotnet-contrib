// <copyright file="DiagnosticsHandlerLoggingStrings.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.HttpTelemetryHandler.Implementation
{
    /// <summary>
    /// Defines names of DiagnosticListener and Write events for DiagnosticHandler.
    /// </summary>
    internal static class DiagnosticsHandlerLoggingStrings
    {
        public const string DiagnosticListenerName = "HttpHandlerDiagnosticListener";
        public const string RequestWriteNameDeprecated = "System.Net.Http.Request";
        public const string ResponseWriteNameDeprecated = "System.Net.Http.Response";

        public const string ExceptionEventName = "System.Net.Http.Exception";
        public const string ActivityName = "System.Net.Http.HttpRequestOut";
        public const string ActivityStartName = "System.Net.Http.HttpRequestOut.Start";

        public const string RequestIdHeaderName = "Request-Id";
        public const string CorrelationContextHeaderName = "Correlation-Context";

        public const string TraceParentHeaderName = "traceparent";
        public const string TraceStateHeaderName = "tracestate";
    }
}
