// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Web;
using System.Web.Routing;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

/// <summary>
/// Helper class for processing http requests.
/// </summary>
internal sealed class HttpRequestRouteHelper
{
    private readonly PropertyFetcher<object> routeFetcher = new("Route");
    private readonly PropertyFetcher<string> routeTemplateFetcher = new("RouteTemplate");

    /// <summary>
    /// Extracts the route template from the <see cref="HttpRequest"/>.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequest"/> being processed.</param>
    /// <returns>The route template or <see langword="null"/>.</returns>
    internal string? GetRouteTemplate(HttpRequestBase request)
    {
        var routeData = request.RequestContext.RouteData;

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
            template = PrepareRouteTemplate(route, routeData);
        }

        return template;
    }

    private static string PrepareRouteTemplate(Route route, RouteData routeData)
    {
        const string controllerToken = "controller";
        const string actionToken = "action";

        var template = route.Url;
        var controller = (string)routeData.Values[controllerToken];
        var action = (string)routeData.Values[actionToken];

        if (!string.IsNullOrWhiteSpace(controller))
        {
            template = template.Replace($"{{{controllerToken}}}", controller);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            template = template.Replace($"{{{actionToken}}}", action);
        }

        // Remove defaults with no values.
        var defaultKeys = route.Defaults.Keys;
        var valueKeys = routeData.Values.Keys;

        foreach (var token in defaultKeys)
        {
            if (valueKeys.Contains(token))
            {
                continue;
            }

            template = template.Replace($"{{{token}}}", string.Empty);
        }

        return template
            .TrimEnd('/'); // Normalizes endings
    }
}
