// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Web.Http;

namespace Examples.Owin.Controllers;

#pragma warning disable CA1515
public class TestController : ApiController
#pragma warning restore CA1515
{
    // GET api/test/{id}
    public string Get(string? id = null)
    {
        return $"id:{id}";
    }
}
