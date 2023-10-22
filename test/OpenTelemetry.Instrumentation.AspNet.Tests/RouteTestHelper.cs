// <copyright file="RouteTestHelper.cs" company="OpenTelemetry Authors">
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
