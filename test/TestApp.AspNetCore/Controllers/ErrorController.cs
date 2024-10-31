// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace TestApp.AspNetCore.Controllers;

[Route("api/[controller]")]
#pragma warning disable CA1515
public class ErrorController : Controller
#pragma warning restore CA1515
{
    // GET api/error
    [HttpGet]
    public string Get()
    {
        throw new Exception("something's wrong!");
    }
}
