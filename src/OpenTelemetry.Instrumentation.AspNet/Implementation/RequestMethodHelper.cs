// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal sealed class RequestMethodHelper
{
    private const string KnownHttpMethodsEnvironmentVariable = "OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS";
    private static readonly char[] SplitChars = new[] { ',' };

    // The value "_OTHER" is used for non-standard HTTP methods.
    // https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md#common-attributes
    private const string OtherHttpMethod = "_OTHER";

    // List of known HTTP methods as per spec.
    private readonly Dictionary<string, string> knownHttpMethods;

    public RequestMethodHelper()
    {
        var suppliedKnownMethods = Environment.GetEnvironmentVariable(KnownHttpMethodsEnvironmentVariable)
            ?.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
        this.knownHttpMethods = suppliedKnownMethods?.Length > 0
            ? suppliedKnownMethods.ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase)
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
    }

    public string GetNormalizedHttpMethod(string method)
    {
        return this.knownHttpMethods.TryGetValue(method, out var normalizedMethod)
            ? normalizedMethod
            : OtherHttpMethod;
    }
}
