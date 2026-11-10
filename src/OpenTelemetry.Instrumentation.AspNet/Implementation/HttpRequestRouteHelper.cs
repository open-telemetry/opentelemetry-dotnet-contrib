// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using System.Web.Routing;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

/// <summary>
/// Helper class for processing http requests.
/// </summary>
internal sealed class HttpRequestRouteHelper
{
    /// <summary>
    /// The maximum number of distinct templates with <c>{controller}</c> and/or <c>{action}</c>
    /// replaced by a route value that are remembered. It is deliberately below the default
    /// per-metric cardinality limit of the OpenTelemetry SDK, although the number of series is
    /// also multiplied by the request method, status code and so on.
    /// </summary>
    internal const int MaxResolvedTemplates = 512;

    private readonly PropertyFetcher<object> routeFetcher = new("Route");
    private readonly PropertyFetcher<string> routeTemplateFetcher = new("RouteTemplate");

    // Templates that had their {controller}/{action} tokens replaced, and that were seen on a request that was
    // actually served. The key is compared ignoring case because MVC and WebAPI resolve controller and action
    // names that way, so the first spelling seen is the one that is reported for all of them.
    private readonly ConcurrentDictionary<string, string> resolvedTemplates = new(StringComparer.OrdinalIgnoreCase);

    // Serializes the additions to resolvedTemplates so that the limit is exact. Lookups do not take the lock.
    private readonly object resolvedTemplatesLock = new();
    private int resolvedTemplatesCount;

    /// <summary>
    /// Extracts the route template from the <see cref="HttpRequest"/>.
    /// </summary>
    /// <remarks>
    /// The <c>controller</c> and <c>action</c> route values of a conventional route are taken from the
    /// URL by System.Web routing before it is known whether such a controller or action exists, so they
    /// are controlled by the client and cannot be used as they are for <c>http.route</c>, which must be
    /// low-cardinality. They are only substituted into the template once the same template has been
    /// seen on a request that completed successfully (which proves the names resolve to something that
    /// exists); until then, and for requests that do not resolve to anything (for example a 404), the
    /// <c>{controller}</c> and <c>{action}</c> tokens are left in place.
    /// </remarks>
    /// <param name="request">The <see cref="HttpRequest"/> being processed.</param>
    /// <param name="isSuccessful">
    /// Whether the request completed successfully (a status code below 400 and no unhandled error).
    /// Only the route values of such requests are trusted to be names of an existing controller and action.
    /// </param>
    /// <returns>The route template or <see langword="null"/>.</returns>
    internal string? GetRouteTemplate(HttpRequestBase request, bool isSuccessful)
    {
        var routeData = request.RequestContext?.RouteData;
        if (routeData == null)
        {
            return null;
        }

        string? template = null;
        if (routeData.Values.TryGetValue("MS_SubRoutes", out var msSubRoutes))
        {
            // WebAPI attribute routing flows here. Use reflection to not take a dependency on microsoft.aspnet.webapi.core\[version]\lib\[framework]\System.Web.Http.

            if (msSubRoutes is Array { Length: >= 1 } attributeRouting)
            {
                // There could be more than one subroute, each with a different method.
                // But the template is the same across them, so we simply take the template
                // from the first route.
                var subRouteData = attributeRouting.GetValue(0);

                _ = this.routeFetcher.TryFetch(subRouteData, out var route);
                _ = this.routeTemplateFetcher.TryFetch(route, out template);
            }
        }
        else if (routeData.Route is Route route)
        {
            // MVC + WebAPI traditional routing & MVC attribute routing flow here.
            template = this.PrepareRouteTemplate(route, routeData, isSuccessful);
        }

        return template;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CompareStringToSubstring(string example, string target, int start, int end)
    {
        if (end - start - 1 != example.Length)
        {
            return false;
        }

        for (var i = 0; i < example.Length; i++)
        {
            if (target[start + 1 + i] != example[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildRouteTemplate(Route route, RouteData routeData, bool substituteRouteValues, out bool substituted)
    {
        const string controllerToken = "controller";
        const string actionToken = "action";

        substituted = false;

        var template = route.Url;
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var controller = routeData.Values[controllerToken] as string;
        var action = routeData.Values[actionToken] as string;
        var hasController = substituteRouteValues && !string.IsNullOrWhiteSpace(controller);
        var hasAction = substituteRouteValues && !string.IsNullOrWhiteSpace(action);
        var sb = new StringBuilder(template.Length);

        var i = 0;
        while (i < template.Length)
        {
            if (template[i] == '{')
            {
                var end = template.IndexOf('}', i + 1);
                if (end != -1)
                {
                    if (hasController && CompareStringToSubstring(controllerToken, template, i, end))
                    {
                        sb.Append(controller);
                        substituted = true;
                    }
                    else if (hasAction && CompareStringToSubstring(actionToken, template, i, end))
                    {
                        sb.Append(action);
                        substituted = true;
                    }
                    else
                    {
                        var defaults = route.Defaults;
                        var values = routeData.Values;
                        var token = template.Substring(i + 1, end - i - 1);

                        if (defaults != null && defaults.ContainsKey(token) && !values.ContainsKey(token))
                        {
                            // Ignore defaults with no values.
                        }
                        else
                        {
                            sb.Append('{').Append(token).Append('}');
                        }
                    }

                    i = end + 1;
                    continue;
                }
            }

            sb.Append(template[i]);
            i++;
        }

        // Normalizes endings by removing trailing slashes.
        var len = sb.Length;
        while (len > 0 && sb[len - 1] == '/')
        {
            len--;
        }

        return sb.ToString(0, len);
    }

    private string PrepareRouteTemplate(Route route, RouteData routeData, bool isSuccessful)
    {
        var template = BuildRouteTemplate(route, routeData, substituteRouteValues: true, out var substituted);
        if (!substituted)
        {
            return template;
        }

        if (this.resolvedTemplates.TryGetValue(template, out var resolved))
        {
            return resolved;
        }

        if (isSuccessful && Volatile.Read(ref this.resolvedTemplatesCount) < MaxResolvedTemplates)
        {
            lock (this.resolvedTemplatesLock)
            {
                // Check again as the template could have been added, or the limit reached, while waiting for the lock.
                if (this.resolvedTemplates.TryGetValue(template, out resolved))
                {
                    return resolved;
                }

                var count = this.resolvedTemplatesCount;
                if (count < MaxResolvedTemplates)
                {
                    this.resolvedTemplates[template] = template;
                    Volatile.Write(ref this.resolvedTemplatesCount, count + 1);
                    return template;
                }
            }
        }

        // The names have not been proven to exist, so they must not be used. Report the template itself.
        return BuildRouteTemplate(route, routeData, substituteRouteValues: false, out _);
    }
}
