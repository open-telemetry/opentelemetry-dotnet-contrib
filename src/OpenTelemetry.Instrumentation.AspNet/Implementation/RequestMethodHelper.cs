// <copyright file="RequestMethodHelper.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal sealed class RequestMethodHelper
{
    private const string KnownHttpMethodsEnvironmentVariable = "OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS";

    // The value "_OTHER" is used for non-standard HTTP methods.
    // https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md#common-attributes
    private const string OtherHttpMethod = "_OTHER";

    // List of known HTTP methods as per spec.
    private readonly Dictionary<string, string> knownHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET"] = "GET",
        ["POST"] = "POST",
        ["PUT"] = "PUT",
        ["DELETE"] = "DELETE",
        ["HEAD"] = "HEAD",
        ["OPTIONS"] = "OPTIONS",
        ["TRACE"] = "TRACE",
        ["PATCH"] = "PATCH",
        ["CONNECT"] = "CONNECT",
    };

    public RequestMethodHelper()
    {
        var suppliedKnownMethods = Environment.GetEnvironmentVariable(KnownHttpMethodsEnvironmentVariable)
            ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (suppliedKnownMethods?.Length > 0)
        {
            this.knownHttpMethods = suppliedKnownMethods.ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);
        }
    }

    public string GetNormalizedHttpMethod(string method)
    {
        return this.knownHttpMethods.TryGetValue(method, out var normalizedMethod)
            ? normalizedMethod
            : OtherHttpMethod;
    }
}
