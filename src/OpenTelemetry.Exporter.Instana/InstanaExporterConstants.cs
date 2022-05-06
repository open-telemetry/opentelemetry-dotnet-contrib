// <copyright file="InstanaExporterConstants.cs" company="OpenTelemetry Authors">
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

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OpenTelemetry.Exporter.Instana.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010051c1562a090fb0c9f391012a32198b5e5d9a60e9b80fa2d7b434c9e5ccb7259bd606e66f9660676afc6692b8cdc6793d190904551d2103b7b22fa636dcbb8208839785ba402ea08fc00c8f1500ccef28bbf599aa64ffb1e1d5dc1bf3420a3777badfe697856e9d52070a50c3ea5821c80bef17ca3acffa28f89dd413f096f898")]

namespace OpenTelemetry.Exporter.Instana
{
    internal class InstanaExporterConstants
    {
#pragma warning disable SA1310 // Field names should not contain underscore
        internal const string OTEL_SPAN_TYPE = "otel";
        internal const string KIND_FIELD = "kind";
        internal const string SERVER_KIND = "server";
        internal const string CLIENT_KIND = "client";
        internal const string PRODUCER_KIND = "producer";
        internal const string CONSUMER_KIND = "consumer";
        internal const string INTERNAL_KIND = "internal";
        internal const string SERVICE_FIELD = "service";
        internal const string OPERATION_FIELD = "operation";
        internal const string TRACE_STATE_FIELD = "trace_state";
        internal const string ERROR_FIELD = "error";
        internal const string ERROR_DETAIL_FIELD = "error_detail";
        internal const string EXCEPTION_FIELD = "exception";
        internal const string TAGS_FIELD = "tags";
        internal const string EVENTS_FIELD = "events";
        internal const string EVENT_NAME_FIELD = "name";
        internal const string EVENT_TIMESTAMP_FIELD = "ts";

        internal const string ENVVAR_INSTANA_ENDPOINT_URL = "INSTANA_ENDPOINT_URL";
        internal const string ENVVAR_INSTANA_AGENT_KEY = "INSTANA_AGENT_KEY";
        internal const string ENVVAR_INSTANA_TIMEOUT = "INSTANA_TIMEOUT";
        internal const int BACKEND_DEFAULT_TIMEOUT = 20000;
        internal const string ENVVAR_INSTANA_EXTRA_HTTP_HEADERS = "INSTANA_EXTRA_HTTP_HEADERS";
        internal const string ENVVAR_INSTANA_ENDPOINT_PROXY = "INSTANA_ENDPOINT_PROXY";
#pragma warning restore SA1310 // Field names should not contain underscore
    }
}
