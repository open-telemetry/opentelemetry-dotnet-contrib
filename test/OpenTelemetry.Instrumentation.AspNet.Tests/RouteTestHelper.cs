// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Web;
using System.Web.Routing;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

internal static class RouteTestHelper
{
    public static HttpContext BuildHttpContext(string url, int routeType, string routeTemplate)
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
                routeData = new RouteData()
                {
                    Route = new Route(routeTemplate, null),
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
                            RouteTemplate = routeTemplate,
                        },
                    },
                };
                routeData.Values.Add(
                    "MS_SubRoutes",
                    value);
                break;
            default:
                throw new NotSupportedException();
        }

        var request = new HttpRequest(string.Empty, url, string.Empty)
        {
            RequestContext = new RequestContext()
            {
                RouteData = routeData,
            },
        };

        return new HttpContext(request, new HttpResponse(new StringWriter()));
    }
}
