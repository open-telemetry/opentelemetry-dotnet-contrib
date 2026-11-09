// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace RouteTests.Controllers;

public class FallbackRouteController : Controller
{
    public IActionResult Show(string? page) => this.Content(page ?? string.Empty);
}
