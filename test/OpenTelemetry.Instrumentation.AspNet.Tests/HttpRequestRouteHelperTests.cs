// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Web;
using System.Web.Routing;
using OpenTelemetry.Instrumentation.AspNet.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class HttpRequestRouteHelperTests
{
    [Fact]
    public void GetRouteTemplate_DoesNotThrow_WhenRouteDefaultsAreNull()
    {
        var request = new HttpRequest(string.Empty, "http://localhost/custom/value", string.Empty)
        {
            RequestContext = new RequestContext
            {
                RouteData = new RouteData
                {
                    Route = new Route("custom/{value}", routeHandler: null),
                },
            },
        };

        var routeHelper = new HttpRequestRouteHelper();

        var template = routeHelper.GetRouteTemplate(new HttpRequestWrapper(request));

        Assert.Equal("custom/{value}", template);
    }

    [Fact]
    public void GetRouteTemplate_DoesNotThrow_WhenControllerAndActionAreMissing()
    {
        var request = new HttpRequest(string.Empty, "http://localhost/custom/value", string.Empty)
        {
            RequestContext = new RequestContext
            {
                RouteData = new RouteData
                {
                    Route = new Route("custom/{controller}/{action}/{value}", routeHandler: null),
                },
            },
        };

        var routeHelper = new HttpRequestRouteHelper();

        var template = routeHelper.GetRouteTemplate(new HttpRequestWrapper(request));

        Assert.Equal("custom/{controller}/{action}/{value}", template);
    }
}
