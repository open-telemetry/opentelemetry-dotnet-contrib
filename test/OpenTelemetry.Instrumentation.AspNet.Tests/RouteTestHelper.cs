// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http;
using System.Web;
using System.Web.Routing;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

internal static class RouteTestHelper
{
    public static HttpContext BuildHttpContext(string url, int routeType, string? routeTemplate, string requestMethod, string? controller, string? action)
    {
        RouteData routeData;
        switch (routeType)
        {
            case 0: // WebForm, no route data.
                routeData = new RouteData();
                break;
            case 1: // Traditional MVC.
            case 2: // Attribute routing MVC.
            case 3: // Traditional WebAPI.

                var routeDefaults = new RouteValueDictionary()
                {
                    // This marks id parameter as optional.
                    // Parameter name should match with the route template used in tests.
                    ["id"] = new { },
                };
                routeData = new RouteData
                {
                    Route = new Route(routeTemplate, routeDefaults, null),
                    Values = { { "controller", controller }, { "action", action } },
                };
                break;
            case 4: // Attribute routing WebAPI.
                routeData = new RouteData();
                var value = new[]
                {
                    new
                    {
                        Route = new
                        {
                            Defaults = new RouteValueDictionary(),
                            RouteTemplate = routeTemplate,
                        },
                    },
                };
                routeData.Values.Add(
                    "MS_SubRoutes",
                    value);
                break;
            case 5: // Multi-method attribute routing WebAPI.
                routeData = new RouteData();
                var httpMethods = new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete };

                var multiMethodSubroutes = httpMethods.Select(method => new
                {
                    Route = new
                    {
                        RouteTemplate = routeTemplate,
                        Defaults = new RouteValueDictionary(),
                        DataTokens = new Dictionary<string, object>
                        {
                            ["actions"] = new[]
                            {
                                new
                                {
                                    SupportedHttpMethods = new[] { method },
                                },
                            },
                        },
                    },
                }).ToArray();

                routeData.Values.Add(
                    "MS_SubRoutes",
                    multiMethodSubroutes);
                break;
            default:
                throw new NotSupportedException();
        }

        var request = new HttpRequest(string.Empty, url, string.Empty)
        {
            RequestContext = new RequestContext
            {
                RouteData = routeData,
            },
        };

        typeof(HttpRequest).GetField("_httpMethod", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(request, requestMethod);

        return new HttpContext(request, new HttpResponse(new StringWriter()));
    }
}
