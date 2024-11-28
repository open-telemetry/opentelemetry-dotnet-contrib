// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace RouteTests.Controllers;

[Area("MyArea")]
#pragma warning disable CA1515
public class ControllerForMyAreaController : Controller
#pragma warning restore CA1515
{
    public IActionResult Default() => this.Ok();

    public IActionResult NonDefault() => this.Ok();
}
