// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace RouteTests.Controllers;

#pragma warning disable CA1515
public class ConventionalRouteController : Controller
#pragma warning restore CA1515
{
    public IActionResult Default() => this.Ok();

#pragma warning disable IDE0060 // Remove unused parameter
    public IActionResult ActionWithParameter(int id) => this.Ok();

    public IActionResult ActionWithStringParameter(string id, int num) => this.Ok();
#pragma warning restore IDE0060 // Remove unused parameter
}
