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

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal sealed class RequestMethodHelper
{
    private const string KnownHttpMethodsEnvironmentVariable = "OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS";

    // List of known HTTP methods in order of expected frequency.
    private readonly string[] knownHttpMethods = new[]
    {
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "HEAD",
        "OPTIONS",
        "TRACE",
        "PATCH",
        "CONNECT",
    };

    public RequestMethodHelper()
    {
        var suppliedKnownMethods = Environment.GetEnvironmentVariable(KnownHttpMethodsEnvironmentVariable)
            ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (suppliedKnownMethods?.Length > 0)
        {
            this.knownHttpMethods = suppliedKnownMethods;
        }
    }

    /// <summary>
    /// Returns whether the method is a known HTTP method.
    /// </summary>
    /// <param name="method">The method to check.</param>
    /// <param name="outMethod">The canonical method or null.</param>
    /// <returns>Returns true if the method is known, else false.</returns>
    public bool TryGetMethod(string method, out string? outMethod)
    {
        foreach (var knownMethod in this.knownHttpMethods)
        {
            if (knownMethod.Equals(method, StringComparison.OrdinalIgnoreCase))
            {
                outMethod = knownMethod;
                return true;
            }
        }

        outMethod = null;
        return false;
    }
}
