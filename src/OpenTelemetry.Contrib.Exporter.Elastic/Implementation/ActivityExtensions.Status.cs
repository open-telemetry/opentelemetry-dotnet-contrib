// <copyright file="ActivityExtensions.Status.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Net;
using OpenTelemetry.Contrib.Exporter.Elastic.Implementation.V2;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation
{
    /// <summary>
    /// Extension methods to create Elastic APM transactions/spans.
    /// </summary>
    internal static partial class ActivityExtensions
    {
        private static HttpStatusCode? GetHttpStatusCode(this Activity activity)
        {
            var statusCode = activity.TagObjects.FirstOrDefault(t => t.Key == "http.status_code");
            if (Enum.TryParse(statusCode.Value?.ToString(), out HttpStatusCode httpStatusCode))
            {
                return httpStatusCode;
            }

            return null;
        }

        private static StatusCode? GetOtelStatusCode(this Activity activity)
        {
            var statusCode = activity.TagObjects.FirstOrDefault(t => t.Key == "otel.status_code");
            if (Enum.TryParse(statusCode.Value?.ToString(), true, out StatusCode otelStatusCode))
            {
                return otelStatusCode;
            }

            return null;
        }

        private static Outcome GetOutcome(HttpStatusCode? httpStatusCode, StatusCode? otelStatusCode)
        {
            if (httpStatusCode.HasValue)
            {
                return ((int)httpStatusCode >= 200) && ((int)httpStatusCode <= 299)
                    ? Outcome.Success
                    : Outcome.Failure;
            }

            if (otelStatusCode.HasValue)
            {
                return otelStatusCode != StatusCode.Error
                    ? Outcome.Success
                    : Outcome.Failure;
            }

            return Outcome.Unknown;
        }
    }
}
