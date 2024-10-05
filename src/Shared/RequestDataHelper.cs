// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal;

internal sealed class RequestDataHelper
{
    private const string KnownHttpMethodsEnvironmentVariable = "OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS";

    // The value "_OTHER" is used for non-standard HTTP methods.
    // https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md#common-attributes
    private const string OtherHttpMethod = "_OTHER";

    private static readonly char[] SplitChars = new[] { ',' };

#if NET8_0_OR_GREATER
    private readonly FrozenDictionary<string, string> knownHttpMethods;
#else
    private readonly Dictionary<string, string> knownHttpMethods;
#endif

    public RequestDataHelper(bool configureByHttpKnownMethodsEnvironmentalVariable)
    {
        var suppliedKnownMethods = configureByHttpKnownMethodsEnvironmentalVariable ? Environment.GetEnvironmentVariable(KnownHttpMethodsEnvironmentVariable)
            ?.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries) : null;
        var knownMethodSet = suppliedKnownMethods?.Length > 0
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

#if NET8_0_OR_GREATER
        this.knownHttpMethods = knownMethodSet.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
#else
        this.knownHttpMethods = knownMethodSet;
#endif
    }

    public void SetHttpMethodTag(Activity activity, string originalHttpMethod)
    {
        var normalizedHttpMethod = this.GetNormalizedHttpMethod(originalHttpMethod);
        activity.SetTag(SemanticConventions.AttributeHttpRequestMethod, normalizedHttpMethod);

        if (originalHttpMethod != normalizedHttpMethod)
        {
            activity.SetTag(SemanticConventions.AttributeHttpRequestMethodOriginal, originalHttpMethod);
        }
    }

    public string GetNormalizedHttpMethod(string method)
    {
        return this.knownHttpMethods.TryGetValue(method, out var normalizedMethod)
            ? normalizedMethod
            : OtherHttpMethod;
    }

    public void SetActivityDisplayName(Activity activity, string originalHttpMethod, string? httpRoute = null)
    {
        // https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/http/http-spans.md#name

        var normalizedHttpMethod = this.GetNormalizedHttpMethod(originalHttpMethod);
        var namePrefix = normalizedHttpMethod == "_OTHER" ? "HTTP" : normalizedHttpMethod;

        activity.DisplayName = string.IsNullOrEmpty(httpRoute) ? namePrefix : $"{namePrefix} {httpRoute}";
    }

    internal static string GetHttpProtocolVersion(Version httpVersion)
    {
        return httpVersion switch
        {
            { Major: 1, Minor: 0 } => "1.0",
            { Major: 1, Minor: 1 } => "1.1",
            { Major: 2, Minor: 0 } => "2",
            { Major: 3, Minor: 0 } => "3",
            _ => httpVersion.ToString(),
        };
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
