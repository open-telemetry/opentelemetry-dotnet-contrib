// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Concurrent;
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

    private static readonly char[] SplitChars = [','];

#if NET
    private readonly FrozenDictionary<string, KnownHttpMethod> knownHttpMethods;
    private readonly KnownHttpMethod otherHttpMethod = new(OtherHttpMethod);
#else
    private readonly Dictionary<string, string> knownHttpMethods;
#endif

    public RequestDataHelper(bool configureByHttpKnownMethodsEnvironmentalVariable)
    {
        var suppliedKnownMethods = configureByHttpKnownMethodsEnvironmentalVariable ? Environment.GetEnvironmentVariable(KnownHttpMethodsEnvironmentVariable)
            ?.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries) : null;

        Dictionary<string, string> knownMethodSet;

        if (suppliedKnownMethods?.Length > 0)
        {
            // The user supplied a custom set of known HTTP methods via OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS,
            // so the normalized method/display name may differ from the framework's defaults.
            this.HasCustomKnownMethods = true;
            knownMethodSet = suppliedKnownMethods.ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            knownMethodSet = new(StringComparer.OrdinalIgnoreCase)
            {
                // See https://github.com/open-telemetry/semantic-conventions/blob/v1.38.0/model/http/registry.yaml
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

            // .NET 9+ has native support for instrumentation, so our custom code doesn't run,
            // but we don't target net9.0 so we cannot use conditional compilation to light-up
            // support for QUERY and only .NET 10+ has support for QUERY itself.
            if (Environment.Version.Major is not 9)
            {
                knownMethodSet["QUERY"] = "QUERY";
            }
        }

#if NET
        this.knownHttpMethods = knownMethodSet.ToFrozenDictionary(
            static p => p.Key,
            static p => new KnownHttpMethod(p.Value),
            StringComparer.OrdinalIgnoreCase);
#else
        this.knownHttpMethods = knownMethodSet;
#endif
    }

    /// <summary>
    /// Gets a value indicating whether a custom set of known HTTP methods was supplied via the
    /// <c>OTEL_INSTRUMENTATION_HTTP_KNOWN_METHODS</c> environment variable.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/> the normalized method (and method-based display name) can
    /// differ from a framework's defaults, so callers that would otherwise defer to native
    /// framework values must not do so.
    /// </remarks>
    public bool HasCustomKnownMethods { get; }

    public void SetHttpMethodTag(Activity activity, string originalHttpMethod)
    {
        var normalizedHttpMethod = this.GetNormalizedHttpMethod(originalHttpMethod);
        activity.SetTag(SemanticConventions.AttributeHttpRequestMethod, normalizedHttpMethod);

        if (originalHttpMethod != normalizedHttpMethod)
        {
            activity.SetTag(SemanticConventions.AttributeHttpRequestMethodOriginal, originalHttpMethod);
        }
    }

    public void SetActivityDisplayNameAndHttpMethodTag(Activity activity, string originalHttpMethod)
    {
        var normalizedHttpMethod = this.GetNormalizedHttpMethod(originalHttpMethod);

        activity.DisplayName = normalizedHttpMethod == OtherHttpMethod ? "HTTP" : normalizedHttpMethod;
        activity.SetTag(SemanticConventions.AttributeHttpRequestMethod, normalizedHttpMethod);

        if (originalHttpMethod != normalizedHttpMethod)
        {
            activity.SetTag(SemanticConventions.AttributeHttpRequestMethodOriginal, originalHttpMethod);
        }
    }

    public void SetHttpMethodTag(ref TagList tags, string originalHttpMethod)
    {
        var normalizedHttpMethod = this.GetNormalizedHttpMethod(originalHttpMethod);
        tags.Add(SemanticConventions.AttributeHttpRequestMethod, normalizedHttpMethod);

        if (originalHttpMethod != normalizedHttpMethod)
        {
            tags.Add(SemanticConventions.AttributeHttpRequestMethodOriginal, originalHttpMethod);
        }
    }

    public string GetNormalizedHttpMethod(string method)
#if NET
        => this.knownHttpMethods.TryGetValue(method, out var knownMethod)
            ? knownMethod.Name
            : OtherHttpMethod;
#else
        => this.knownHttpMethods.TryGetValue(method, out var normalizedMethod)
            ? normalizedMethod
            : OtherHttpMethod;
#endif

    public void SetActivityDisplayName(Activity activity, string originalHttpMethod, string? httpRoute = null)
        => activity.DisplayName = this.GetActivityDisplayName(originalHttpMethod, httpRoute);

    public string GetActivityDisplayName(string originalHttpMethod, string? httpRoute = null)
    {
        // https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/http/http-spans.md#name

#if NET
        var knownMethod = this.knownHttpMethods.TryGetValue(originalHttpMethod, out var method)
            ? method
            : this.otherHttpMethod;

        return string.IsNullOrEmpty(httpRoute)
            ? knownMethod.DisplayName
            : knownMethod.GetDisplayName(httpRoute);
#else
        var normalizedHttpMethod = this.GetNormalizedHttpMethod(originalHttpMethod);
        var namePrefix = normalizedHttpMethod == OtherHttpMethod ? "HTTP" : normalizedHttpMethod;

        return string.IsNullOrEmpty(httpRoute) ? namePrefix : $"{namePrefix} {httpRoute}";
#endif
    }

    internal static string GetHttpProtocolVersion(Version httpVersion) => httpVersion switch
    {
        { Major: 1, Minor: 0 } => "1.0",
        { Major: 1, Minor: 1 } => "1.1",
        { Major: 2, Minor: 0 } => "2",
        { Major: 3, Minor: 0 } => "3",
        _ => httpVersion.ToString(),
    };

    internal static string GetHttpProtocolVersion(string protocol) => protocol switch
    {
        "HTTP/1.0" => "1.0",
        "HTTP/1.1" => "1.1",
        "HTTP/2" => "2",
        "HTTP/3" => "3",
        _ => protocol,
    };

#if NET
    private sealed class KnownHttpMethod
    {
        // Caches the display name for each route requested with this (normalized) HTTP method.
        // The number of entries is bounded by the number of routes in the app. The cache is
        // keyed by the route alone and created on first use, as many instances (e.g. those used
        // for HTTP clients, which have no routes) are never used with a route.
        private ConcurrentDictionary<string, string>? routeDisplayNames;

        public KnownHttpMethod(string name)
        {
            this.Name = name;
            this.DisplayName = name == OtherHttpMethod ? "HTTP" : name;
        }

        /// <summary>
        /// Gets the normalized name of the HTTP method.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the display name to use for requests using the HTTP method that do not have a route.
        /// </summary>
        public string DisplayName { get; }

        public string GetDisplayName(string httpRoute)
        {
            var cache = this.routeDisplayNames ?? LazyInitializer.EnsureInitialized(ref this.routeDisplayNames, static () => new());
            return cache.GetOrAdd(httpRoute, static (route, prefix) => $"{prefix} {route}", this.DisplayName);
        }
    }
#endif
}
