// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal sealed class RequestDataHelper
{
    private const string KnownHttpMethodsEnvironmentVariable = "OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS";

    // The value "_OTHER" is used for non-standard HTTP methods.
    // https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md#common-attributes
    private const string OtherHttpMethod = "_OTHER";

    private static readonly char[] SplitChars = new[] { ',' };

    // List of known HTTP methods as per spec.
    private readonly Dictionary<string, string> knownHttpMethods;

    public RequestDataHelper()
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

    public static string GetHttpProtocolVersion(HttpRequest request)
    {
        return GetHttpProtocolVersion(request.ServerVariables["SERVER_PROTOCOL"]);
    }

    public string GetNormalizedHttpMethod(string method)
    {
        return this.knownHttpMethods.TryGetValue(method, out var normalizedMethod)
            ? normalizedMethod
            : OtherHttpMethod;
    }

    internal static string GetHttpProtocolVersion(string protocol)
    {
        return protocol switch
        {
            "HTTP/1.1" => "1.1",
            "HTTP/2" => "2",
            "HTTP/3" => "3",
            _ => protocol,
        };
    }
}
