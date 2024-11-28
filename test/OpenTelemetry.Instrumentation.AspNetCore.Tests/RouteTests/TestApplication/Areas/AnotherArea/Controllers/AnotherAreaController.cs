// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace RouteTests.Controllers;

[Area("AnotherArea")]
#pragma warning disable CA1515
public class AnotherAreaController : Controller
#pragma warning restore CA1515
{
    public IActionResult Index() => this.Ok();
}
