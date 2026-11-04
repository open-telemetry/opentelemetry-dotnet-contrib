// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Web;
using System.Web.Routing;
using OpenTelemetry.Instrumentation.AspNet.Implementation;

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

        var template = routeHelper.GetRouteTemplate(new HttpRequestWrapper(request), isSuccessful: true);

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

        var template = routeHelper.GetRouteTemplate(new HttpRequestWrapper(request), isSuccessful: true);

        Assert.Equal("custom/{controller}/{action}/{value}", template);
    }

    [Fact]
    public void GetRouteTemplate_DoesNotUseRouteValuesOfRequestsThatWereNotServed()
    {
        var routeHelper = new HttpRequestRouteHelper();

        // The controller and action route values are taken from the URL before it is known whether they exist.
        var templates = new HashSet<string?>();
        for (var i = 0; i < 2001; i++)
        {
            templates.Add(routeHelper.GetRouteTemplate(CreateConventionalRequest($"x{i}", $"y{i}", id: null), isSuccessful: false));
        }

        Assert.Equal("{controller}/{action}", Assert.Single(templates));
    }

    [Fact]
    public void GetRouteTemplate_DoesNotUseRouteValuesOfUnknownNamesWhenOtherNamesAreKnown()
    {
        var routeHelper = new HttpRequestRouteHelper();

        Assert.Equal("Home/Index/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Home", "Index", "1"), isSuccessful: true));

        // A name that has not been served is not used, even though others have been, and neither is a partial match.
        Assert.Equal("{controller}/{action}/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("x", "y", "1"), isSuccessful: false));
        Assert.Equal("{controller}/{action}/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Home", "Missing", "1"), isSuccessful: false));
        Assert.Equal("{controller}/{action}/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Missing", "Index", "1"), isSuccessful: false));
    }

    [Fact]
    public void GetRouteTemplate_UsesRouteValuesOnceTheyHaveBeenServed()
    {
        var routeHelper = new HttpRequestRouteHelper();

        // Not served yet, so the template is reported.
        Assert.Equal("{controller}/{action}/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Orders", "Details", "1"), isSuccessful: false));

        Assert.Equal("Orders/Details/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Orders", "Details", "1"), isSuccessful: true));

        // Once served, the name is used for every outcome and for every id.
        Assert.Equal("Orders/Details/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Orders", "Details", "2"), isSuccessful: true));
        Assert.Equal("Orders/Details/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Orders", "Details", "3"), isSuccessful: false));

        // A different shape of the template (no id) is only used once it has been served as well.
        Assert.Equal("{controller}/{action}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Orders", "Details", id: null), isSuccessful: false));
        Assert.Equal("Orders/Details", routeHelper.GetRouteTemplate(CreateConventionalRequest("Orders", "Details", id: null), isSuccessful: true));
        Assert.Equal("Orders/Details", routeHelper.GetRouteTemplate(CreateConventionalRequest("Orders", "Details", id: null), isSuccessful: false));
    }

    [Fact]
    public void GetRouteTemplate_ReportsOneSpellingOfEachControllerAndAction()
    {
        var routeHelper = new HttpRequestRouteHelper();

        Assert.Equal("Home/Index/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("Home", "Index", "1"), isSuccessful: true));

        // MVC and WebAPI resolve the names ignoring case, so these requests succeed too.
        var templates = new HashSet<string?>();
        foreach (var (controller, action) in new[] { ("HOME", "INDEX"), ("HOME", "index"), ("home", "INDEX"), ("home", "index"), ("Home", "Index") })
        {
            templates.Add(routeHelper.GetRouteTemplate(CreateConventionalRequest(controller, action, "1"), isSuccessful: true));
        }

        Assert.Equal("Home/Index/{id}", Assert.Single(templates));
    }

    [Fact]
    public void GetRouteTemplate_LimitsTheNumberOfRouteValuesThatAreUsed()
    {
        var routeHelper = new HttpRequestRouteHelper();

        // Even if the request is reported as served for every name, the number of names that are used is bounded.
        var templates = new HashSet<string?>();
        for (var i = 0; i < HttpRequestRouteHelper.MaxResolvedTemplates * 2; i++)
        {
            templates.Add(routeHelper.GetRouteTemplate(CreateConventionalRequest($"x{i}", "Index", "1"), isSuccessful: true));
        }

        Assert.Equal(HttpRequestRouteHelper.MaxResolvedTemplates + 1, templates.Count);
        Assert.Contains("{controller}/{action}/{id}", templates);

        // The names that were used before the limit was reached continue to be used.
        Assert.Equal("x0/Index/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("x0", "Index", "1"), isSuccessful: false));
        Assert.Equal("{controller}/{action}/{id}", routeHelper.GetRouteTemplate(CreateConventionalRequest("late", "Index", "1"), isSuccessful: true));
    }

    [Fact]
    public async Task GetRouteTemplate_LimitsTheNumberOfRouteValuesThatAreUsedWhenRequestsAreConcurrent()
    {
        const int Threads = 16;
        const int RequestsPerThread = HttpRequestRouteHelper.MaxResolvedTemplates;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var routeHelper = new HttpRequestRouteHelper();
            var results = new string?[Threads * RequestsPerThread];

            // Release all of the threads at once so that they race to add names while the limit is reached.
            using var start = new ManualResetEventSlim(initialState: false);
            var tasks = new Task[Threads];
            for (var t = 0; t < Threads; t++)
            {
                var thread = t;
                tasks[t] = Task.Factory.StartNew(
                    () =>
                    {
                        start.Wait();
                        for (var i = 0; i < RequestsPerThread; i++)
                        {
                            var index = (thread * RequestsPerThread) + i;
                            results[index] = routeHelper.GetRouteTemplate(CreateConventionalRequest($"c{index}", "Index", "1"), isSuccessful: true);
                        }
                    },
                    TestContext.Current.CancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            start.Set();
            await Task.WhenAll(tasks);

            var used = results.Where(r => r is not "{controller}/{action}/{id}").ToList();

            Assert.Equal(HttpRequestRouteHelper.MaxResolvedTemplates, used.Count);
            Assert.Equal(HttpRequestRouteHelper.MaxResolvedTemplates, used.Distinct().Count());

            // Every name that was used is remembered, and no others.
            var remembered = Enumerable.Range(0, results.Length)
                .Count(i => routeHelper.GetRouteTemplate(CreateConventionalRequest($"c{i}", "Index", "1"), isSuccessful: false) is not "{controller}/{action}/{id}");

            Assert.Equal(HttpRequestRouteHelper.MaxResolvedTemplates, remembered);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetRouteTemplate_DoesNotDependOnTheOutcomeForTemplatesWithoutControllerOrAction(bool isSuccessful)
    {
        var routeHelper = new HttpRequestRouteHelper();

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

        Assert.Equal("custom/{value}", routeHelper.GetRouteTemplate(new HttpRequestWrapper(request), isSuccessful));
    }

    private static HttpRequestWrapper CreateConventionalRequest(string controller, string action, string? id)
    {
        // Equivalent of routes.MapRoute("Default", "{controller}/{action}/{id}", new { controller = "Home", action = "Index", id = UrlParameter.Optional })
        var route = new Route(
            "{controller}/{action}/{id}",
            new RouteValueDictionary
            {
                ["controller"] = "Home",
                ["action"] = "Index",
                ["id"] = new { }, // Marks the parameter as optional, like UrlParameter.Optional.
            },
            routeHandler: null);

        var routeData = new RouteData { Route = route };
        routeData.Values["controller"] = controller;
        routeData.Values["action"] = action;
        if (id is not null)
        {
            routeData.Values["id"] = id;
        }

        var request = new HttpRequest(string.Empty, $"http://localhost/{controller}/{action}/{id}", string.Empty)
        {
            RequestContext = new RequestContext { RouteData = routeData },
        };

        return new HttpRequestWrapper(request);
    }
}
